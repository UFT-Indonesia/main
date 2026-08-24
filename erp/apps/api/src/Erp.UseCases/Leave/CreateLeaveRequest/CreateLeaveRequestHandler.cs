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

        // One open request per employee at a time.
        if (await leaveRequests.AnyAsync(new PendingLeaveForEmployeeSpec(employeeId), ct))
        {
            return new Result<LeaveRequestResult>.Error(
                "leave.pending_exists", "This employee already has a pending leave request.");
        }

        var startDate = LocalDate.FromDateOnly(command.StartDate);
        var endDate = LocalDate.FromDateOnly(command.EndDate);

        // New leave cannot double-book dates that are already approved.
        if (await leaveRequests.AnyAsync(new ApprovedLeaveOverlappingSpec(employeeId, startDate, endDate), ct))
        {
            return new Result<LeaveRequestResult>.Error(
                "leave.overlaps_approved", "The requested dates overlap an already approved leave.");
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
                command.Caller.UserId,
                now);

            // Nobody outranks an Owner, so their leave is recorded already-approved — it is a
            // note on the calendar rather than something awaiting a decision.
            if (LeaveRules.IsAutoApproved(employee.Role))
            {
                request.Approve(command.Caller.UserId, command.Caller.Name, now);
            }
        }
        catch (DomainException ex)
        {
            return new Result<LeaveRequestResult>.Error(ex.Code ?? "leave.validation", ex.Message);
        }

        await leaveRequests.AddAsync(request, ct);

        // An Owner's leave is approved right here rather than by a later decision, so this is
        // the only place its approval can reach attendance from.
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
