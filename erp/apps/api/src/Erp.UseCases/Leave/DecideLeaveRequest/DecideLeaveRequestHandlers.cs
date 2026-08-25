using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Leave.DecideLeaveRequest;

// Domain-level lifecycle violations (already decided, not cancellable) throw
// DomainException and bubble to the global exception handler as 400s.

public static class ApproveLeaveRequestHandler
{
    public static Task<Result<LeaveRequestResult>> Handle(
        ApproveLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct) =>
        DecideLeaveRequestService.DecideAsync(
            command.LeaveRequestId,
            command.Caller,
            DecisionKind.Approval,
            (request, _, now) => request.Approve(command.Caller.UserId, command.Caller.Name, now),
            leaveRequests,
            employees,
            clock,
            bus,
            ct,
            // Authoritative quota check. The same request passed this on the way in, but an
            // override lowered or a probation extended since then must still stop it here.
            guard: (request, subject, today) => LeaveQuotaGuard.CheckAsync(
                subject, request.Type, request.StartDate, request.EndDate, leaveRequests, today, ct));
}

public static class DenyLeaveRequestHandler
{
    public static Task<Result<LeaveRequestResult>> Handle(
        DenyLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct) =>
        DecideLeaveRequestService.DecideAsync(
            command.LeaveRequestId,
            command.Caller,
            DecisionKind.Approval,
            (request, _, now) => request.Deny(command.Caller.UserId, command.Caller.Name, now, command.Note),
            leaveRequests,
            employees,
            clock,
            bus,
            ct);
}

public static class CancelLeaveRequestHandler
{
    public static Task<Result<LeaveRequestResult>> Handle(
        CancelLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct) =>
        DecideLeaveRequestService.DecideAsync(
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
            clock,
            bus,
            ct);
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
        IClock clock,
        IMessageBus bus,
        CancellationToken ct,
        Func<LeaveRequest, Employee, LocalDate, Task<(string Code, string Message)?>>? guard = null)
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

        if (guard is not null)
        {
            var blocked = await guard(request, subject, DisplayZone.Today(clock));
            if (blocked is { } violation)
            {
                return new Result<LeaveRequestResult>.Error(violation.Code, violation.Message);
            }
        }

        decide(request, subject, clock.GetCurrentInstant());
        await leaveRequests.UpdateAsync(request, ct);
        await LeaveRequestEventPublisher.PublishAsync(request, bus);

        var (canDecide, canCancel) = LeaveRequestResult.PermissionsFor(caller, request, subject);
        return new Result<LeaveRequestResult>.Success(
            LeaveRequestResult.From(
                request,
                // Single-request responses do not run the yearly rollup query.
                approvedWorkdaysThisYear: null,
                employeeFullName: subject.FullName,
                canDecide: canDecide,
                canCancel: canCancel,
                // Only reachable once authority to decide or cancel has been established.
                canReadDetails: true));
    }
}
