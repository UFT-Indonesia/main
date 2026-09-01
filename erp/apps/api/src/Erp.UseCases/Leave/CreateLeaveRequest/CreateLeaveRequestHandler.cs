using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Leave.CreateLeaveRequest;

public static class CreateLeaveRequestHandler
{
    public static async Task<Result<LeaveRequestResult>> Handle(
        CreateLeaveRequestCommand command,
        IReadRepository<Employee> employees,
        IRepository<LeaveRequest> leaveRequests,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (!Enum.TryParse<LeaveType>(command.Type, ignoreCase: true, out var type)
            || !Enum.IsDefined(type))
        {
            return new Result<LeaveRequestResult>.Error(
                "leave.type", "Leave type must be Annual, Sick, Permission, or Unpaid.");
        }

        var employeeId = new EmployeeId(command.EmployeeId);
        var employee = await employees.GetByIdAsync(employeeId, ct);
        if (employee is null)
        {
            return new Result<LeaveRequestResult>.NotFound("Employee was not found.");
        }

        if (employee.Status == EmployeeStatus.Terminated)
        {
            return new Result<LeaveRequestResult>.Error(
                "leave.employee_terminated", "Cannot file leave for a terminated employee.");
        }

        if (!LeaveRules.CanFileFor(command.Caller, employee))
        {
            return new Result<LeaveRequestResult>.Error(
                ResultErrors.Forbidden, "You cannot file leave for this employee.");
        }

        // A capped number of undecided requests per calendar month, counted by filing date.
        // Owners are exempt: their leave is auto-approved, so it never queues up on anyone.
        if (employee.Role != EmployeeRole.Owner && command.Caller.Role != EmployeeRole.Owner)
        {
            var monthStart = DisplayZone.Today(clock).With(DateAdjusters.StartOfMonth);
            var pendingThisMonth = await leaveRequests.CountAsync(
                new PendingLeaveFiledBetweenSpec(
                    employeeId,
                    monthStart.AtStartOfDayInZone(DisplayZone.Jakarta).ToInstant(),
                    monthStart.PlusMonths(1).AtStartOfDayInZone(DisplayZone.Jakarta).ToInstant()),
                ct);

            if (pendingThisMonth >= LeaveRules.MaxPendingRequestsPerMonth)
            {
                return new Result<LeaveRequestResult>.Error(
                    "leave.pending_limit",
                    $"{employee.FullName} already has {LeaveRules.MaxPendingRequestsPerMonth} leave "
                    + "request(s) awaiting a decision this month. Wait for one to be decided first.");
            }
        }

        var startDate = LocalDate.FromDateOnly(command.StartDate);
        var endDate = LocalDate.FromDateOnly(command.EndDate);

        // New leave cannot double-book dates that are already approved.
        if (await leaveRequests.AnyAsync(new ApprovedLeaveOverlappingSpec(employeeId, startDate, endDate), ct))
        {
            return new Result<LeaveRequestResult>.Error(
                "leave.overlaps_approved", "The requested dates overlap an already approved leave.");
        }

        var today = DisplayZone.Today(clock);

        // Unpaid is the probationary counterpart of Annual: on probation you get Sick/Permission/
        // Unpaid, once confirmed you get Sick/Permission/Annual. Deliberately checked here and not
        // in LeaveQuotaGuard, which also runs at approval — filing date decides, so a probationer
        // who graduates before their manager gets round to it still has an approvable request.
        if (type == LeaveType.Unpaid && !employee.IsOnProbation(today))
        {
            return new Result<LeaveRequestResult>.Error(
                "leave.unpaid_not_on_probation",
                $"{employee.FullName} is not on probation; unpaid leave does not apply.");
        }

        // Fast feedback on the way in. The authoritative check is on approval — a quota lowered
        // while this request sits pending must not be approvable past.
        var overQuota = await LeaveQuotaGuard.CheckAsync(
            employee, type, startDate, endDate, leaveRequests, today, ct);
        if (overQuota is { } violation)
        {
            return new Result<LeaveRequestResult>.Error(violation.Code, violation.Message);
        }

        LeaveRequest request;
        try
        {
            var now = clock.GetCurrentInstant();
            request = LeaveRequest.Create(
                employeeId,
                type,
                startDate,
                endDate,
                command.Reason,
                command.Attachment,
                command.Caller.UserId,
                now);

            // Nobody outranks an Owner, so their own leave is recorded already-approved. An
            // Owner filing for someone else gets the same treatment: they could always approve
            // it, but the filer-can't-decide-their-own-request rule would otherwise leave it
            // permanently stuck on Pending.
            if (LeaveRules.IsAutoApproved(employee.Role, command.Caller.Role))
            {
                request.Approve(command.Caller.UserId, command.Caller.Name, now);
            }
        }
        catch (DomainException ex)
        {
            return new Result<LeaveRequestResult>.Error(ex.Code ?? "leave.validation", ex.Message);
        }

        await leaveRequests.AddAsync(request, ct);

        // Auto-approved leave is approved right here rather than by a later decision, so this
        // is the only place its approval can reach attendance from.
        await LeaveRequestEventPublisher.PublishAsync(request, bus);

        var (canDecide, canCancel) = LeaveRequestResult.PermissionsFor(command.Caller, request, employee);
        return new Result<LeaveRequestResult>.Success(
            LeaveRequestResult.From(
                request,
                // Single-request responses do not run the yearly rollup query.
                approvedWorkdaysThisYear: null,
                employeeFullName: employee.FullName,
                canDecide: canDecide,
                canCancel: canCancel,
                // CanFileFor already passed, which implies the filer may read what they wrote.
                canReadDetails: true));
    }
}
