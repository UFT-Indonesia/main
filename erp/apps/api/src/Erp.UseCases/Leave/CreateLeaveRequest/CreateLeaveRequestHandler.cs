using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Leave.Common;
using NodaTime;

namespace Erp.UseCases.Leave.CreateLeaveRequest;

public static class CreateLeaveRequestHandler
{
    public static async Task<Result<LeaveRequestResult>> Handle(
        CreateLeaveRequestCommand command,
        IReadRepository<Employee> employees,
        IRepository<LeaveRequest> leaveRequests,
        IClock clock,
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
            request = LeaveRequest.Create(
                employeeId,
                type,
                startDate,
                endDate,
                command.Reason,
                command.RequestedByUserId,
                clock.GetCurrentInstant());
        }
        catch (DomainException ex)
        {
            return new Result<LeaveRequestResult>.Error(ex.Code ?? "leave.validation", ex.Message);
        }

        await leaveRequests.AddAsync(request, ct);

        return new Result<LeaveRequestResult>.Success(
            LeaveRequestResult.From(request, employeeFullName: employee.FullName));
    }
}
