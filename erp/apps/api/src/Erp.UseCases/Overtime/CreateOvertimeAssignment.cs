using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Overtime.Common;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Erp.UseCases.Overtime.CreateOvertimeAssignment;

/// <summary>
/// <paramref name="StartTime"/> is ignored-by-contract on a weekday (always 18:30) and required
/// on a day off; whether the date is a day off is decided here from the holiday calendar and
/// stored on the assignment.
/// </summary>
public sealed record CreateOvertimeAssignmentCommand(
    Guid EmployeeId, DateOnly Date, TimeOnly? StartTime, TimeOnly EndTime, Caller Caller);

public static class CreateOvertimeAssignmentHandler
{
    public static async Task<Result<OvertimeAssignmentResult>> Handle(
        CreateOvertimeAssignmentCommand command,
        IReadRepository<Employee> employees,
        IRepository<OvertimeAssignment> assignments,
        IReadRepository<OvertimeAssignment> assignmentsRead,
        IReadRepository<GajiPremiPeriod> periods,
        IReadRepository<AttendanceLog> logs,
        IRepository<AttendanceDay> days,
        AttendanceDayPolicy policy,
        IOptions<OvertimeOptions> options,
        IClock clock,
        CancellationToken ct)
    {
        var employee = await employees.GetByIdAsync(new EmployeeId(command.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<OvertimeAssignmentResult>.NotFound("Employee was not found.");
        }

        if (!OvertimeRules.CanAssign(command.Caller, employee))
        {
            return new Result<OvertimeAssignmentResult>.Error(
                ResultErrors.Forbidden, "You cannot assign overtime to this employee.");
        }

        if (employee.Status == EmployeeStatus.Terminated)
        {
            return new Result<OvertimeAssignmentResult>.Error(
                "overtime.employee_terminated", "Cannot assign overtime to a terminated employee.");
        }

        var date = LocalDate.FromDateOnly(command.Date);
        if (await periods.AnyAsync(new ClosedPeriodByStartSpec(GajiPremiPeriod.StartOf(date)), ct))
        {
            return new Result<OvertimeAssignmentResult>.Error(
                "overtime.period_closed", "That date is in a closed Gaji Premi period; only a rapel can still claim it.");
        }

        if (await assignmentsRead.AnyAsync(new OvertimeOwningPunchesSpec(employee.Id, date, date), ct))
        {
            return new Result<OvertimeAssignmentResult>.Error(
                "overtime.duplicate_date", $"{employee.FullName} already has overtime on that date.");
        }

        // Pending leave is not checked: an approved leave never blocks overtime, and the
        // reverse (overtime blocks leave) is enforced on the leave side.
        var assignment = OvertimeAssignment.Create(
            employee.Id,
            date,
            command.StartTime is { } start ? LocalTime.FromTimeOnly(start) : null,
            LocalTime.FromTimeOnly(command.EndTime),
            isDayOff: !policy.IsWorkday(date),
            command.Caller.UserId,
            command.Caller.Name,
            clock.GetCurrentInstant(),
            autoApprove: LeaveRules.IsAutoApproved(employee.Role, command.Caller.Role));

        await assignments.AddAsync(assignment, ct);

        // Punches may already exist for the date — they now belong to the overtime.
        await OvertimeEvaluator.RecomputeAroundAsync(assignment, logs, days, assignmentsRead, policy, ct);

        return new Result<OvertimeAssignmentResult>.Success(
            await OvertimeMapper.DescribeAsync(assignment, employee, command.Caller, logs, policy, options, ct));
    }
}
