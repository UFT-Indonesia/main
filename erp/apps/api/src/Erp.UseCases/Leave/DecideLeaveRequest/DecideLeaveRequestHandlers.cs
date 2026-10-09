using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Payroll.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Leave.DecideLeaveRequest;

// Domain-level lifecycle violations (already decided, not cancellable) throw
// DomainException and bubble to the global exception handler as 400s.

public static class ApproveLeaveRequestHandler
{
    public static async Task<Result<LeaveRequestResult>> Handle(
        ApproveLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        IReadRepository<LeaveDeductionMonth> months,
        IReadRepository<LeaveDeductionLine> lines,
        AttendanceDayPolicy policy,
        IClock clock,
        IMessageBus bus,
        IPayrollLock payrollLock,
        ClosedMonthLedger ledger,
        CancellationToken ct)
    {
        // Waits out a month being closed, then reads the closed months fresh.
        await payrollLock.AcquireSharedAsync(ct);

        return await DecideLeaveRequestService.DecideAsync(
            command.LeaveRequestId,
            command.Caller,
            DecisionKind.Approval,
            (request, _, now) => request.Approve(command.Caller.UserId, command.Caller.Name, now),
            leaveRequests,
            employees,
            policy,
            clock,
            bus,
            ledger,
            ct,
            // Authoritative quota check. The same request passed this on the way in, but an
            // override lowered or a probation extended since then must still stop it here.
            guard: (request, subject, today) => LeaveQuotaGuard.CheckAsync(
                subject, request.Type, request.StartDate, request.EndDate,
                request.HalfDay, request.StartHour, request.EndHour, policy, leaveRequests, months, lines, today,
                clock.GetCurrentInstant(), ct));
    }
}

public static class DenyLeaveRequestHandler
{
    public static Task<Result<LeaveRequestResult>> Handle(
        DenyLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        AttendanceDayPolicy policy,
        IClock clock,
        IMessageBus bus,
        ClosedMonthLedger ledger,
        CancellationToken ct) =>
        DecideLeaveRequestService.DecideAsync(
            command.LeaveRequestId,
            command.Caller,
            DecisionKind.Approval,
            (request, _, now) => request.Deny(command.Caller.UserId, command.Caller.Name, now, command.Note),
            leaveRequests,
            employees,
            policy,
            clock,
            bus,
            ledger,
            ct);
}

public static class CancelLeaveRequestHandler
{
    public static async Task<Result<LeaveRequestResult>> Handle(
        CancelLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        AttendanceDayPolicy policy,
        IClock clock,
        IMessageBus bus,
        IPayrollLock payrollLock,
        ClosedMonthLedger ledger,
        CancellationToken ct)
    {
        // Waits out a month being closed, then reads the closed months fresh.
        await payrollLock.AcquireSharedAsync(ct);

        return await DecideLeaveRequestService.DecideAsync(
            command.LeaveRequestId,
            command.Caller,
            DecisionKind.Cancellation,
            // Cancelling your own leave is a withdrawal; anyone else with the standing to
            // cancel it is pulling the employee back to work. Derived from who is acting
            // rather than asked for, so it cannot be mislabelled by the caller.
            (request, subject, now) => request.Cancel(
                command.Caller.UserId,
                command.Caller.Name,
                now,
                command.Note,
                OrgScope.IsSelf(command.Caller, subject)
                    ? LeaveCancellationReason.WithdrawnByEmployee
                    : LeaveCancellationReason.RecalledForWork),
            leaveRequests,
            employees,
            policy,
            clock,
            bus,
            ledger,
            ct,
            // Approved leave in a closed payroll month is frozen; only the Owner may cancel it, as a
            // correction with a reason (GSS03 follow-up Q8). A Pending one costs nothing yet, so
            // withdrawing it stays allowed.
            correctionReason: command.Note,
            lockedByPayroll: request => request.Status == LeaveRequestStatus.Approved);
    }
}

internal enum DecisionKind
{
    /// <summary>Approve or deny — requires authority over the subject, and never the requester.</summary>
    Approval,

    /// <summary>Cancel — the subject may always cancel their own, requester or not.</summary>
    Cancellation,
}

internal static class DecideLeaveRequestService
{
    internal static async Task<Result<LeaveRequestResult>> DecideAsync(
        Guid leaveRequestId,
        Caller caller,
        DecisionKind kind,
        Action<LeaveRequest, Employee, Instant> decide,
        IRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        AttendanceDayPolicy policy,
        IClock clock,
        IMessageBus bus,
        ClosedMonthLedger ledger,
        CancellationToken ct,
        Func<LeaveRequest, Employee, LocalDate, Task<QuotaCheck>>? guard = null,
        string? correctionReason = null,
        Func<LeaveRequest, bool>? lockedByPayroll = null)
    {
        var request = await leaveRequests.FirstOrDefaultAsync(
            new LeaveRequestByIdSpec(new LeaveRequestId(leaveRequestId)), ct);
        if (request is null)
        {
            return new Result<LeaveRequestResult>.NotFound("Leave request was not found.");
        }

        // Loaded explicitly rather than off the navigation: authority hinges on the subject's
        // current role and reporting line, so it should not depend on an Include staying put.
        var subject = await employees.GetByIdAsync(request.EmployeeId, ct);
        if (subject is null)
        {
            return new Result<LeaveRequestResult>.NotFound("The employee this request belongs to was not found.");
        }

        var permitted = kind == DecisionKind.Cancellation
            ? LeaveRules.CanCancel(caller, subject)
            : LeaveRules.CanDecideFor(caller, subject)
              && !LeaveRules.IsRequester(caller, request.RequestedByUserId);

        if (!permitted)
        {
            return new Result<LeaveRequestResult>.Error(
                ResultErrors.Forbidden, "You cannot decide this leave request.");
        }

        var closedMonth = await ledger.FirstClosedMonthAsync(request.StartDate, request.EndDate, ct);
        LocalDate? correctionMonth = null;
        if (closedMonth is { } locked && lockedByPayroll is not null && lockedByPayroll(request))
        {
            if (caller.Role != EmployeeRole.Owner)
            {
                return new Result<LeaveRequestResult>.Error(LeavePayrollLock.Code, LeavePayrollLock.OwnerOnlyMessage(locked));
            }

            if (string.IsNullOrWhiteSpace(correctionReason))
            {
                return new Result<LeaveRequestResult>.Error(LeavePayrollLock.ReasonCode, LeavePayrollLock.ReasonMessage);
            }

            correctionMonth = locked;
        }

        decimal? overQuotaDays = null;
        if (guard is not null)
        {
            var check = await guard(request, subject, DisplayZone.Today(clock));
            if (check.Violation is { } violation)
            {
                return new Result<LeaveRequestResult>.Error(violation.Code, violation.Message);
            }

            overQuotaDays = check.OverQuotaDays;
        }

        var now = clock.GetCurrentInstant();
        decide(request, subject, now);

        if (correctionMonth is { } month)
        {
            request.RecordCorrection(correctionReason!, caller.Name, now, month);
        }

        await leaveRequests.UpdateAsync(request, ct);

        // Days landing in an already-closed month are stamped there (follow-up Q6); a correction also
        // supersedes what it removed and moves the money to the first open month (Q8/Q9).
        if (closedMonth is not null)
        {
            var plan = await ledger.PlanAsync(subject, request, DisplayZone.Today(clock), tracked: true, ct);
            await ledger.ApplyAsync(
                plan,
                request,
                correctionMonth is null ? null : new PayrollCorrection(correctionReason!, caller.UserId, caller.Name),
                now,
                ct);
        }

        await LeaveRequestEventPublisher.PublishAsync(request, bus);

        var (canDecide, canCancel, canEdit) = LeaveRequestResult.PermissionsFor(caller, request, subject, closedMonth);
        return new Result<LeaveRequestResult>.Success(
            LeaveRequestResult.From(
                request,
                policy,
                // Single-request responses do not run the yearly rollup query.
                approvedWorkdaysThisYear: null,
                employeeFullName: subject.FullName,
                canDecide: canDecide,
                canCancel: canCancel,
                canEdit: canEdit,
                // Only reachable once authority to decide or cancel has been established.
                canReadDetails: true,
                overQuotaDays: overQuotaDays,
                payroll: LeaveRequestResult.PayrollStateFor(caller, request, subject, closedMonth)));
    }
}
