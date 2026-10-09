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

namespace Erp.UseCases.Leave.GetLeaveOverQuota;

/// <summary>The form's own shape, before anything is filed — see <see cref="GetLeaveOverQuotaHandler"/>.</summary>
public sealed record GetLeaveOverQuotaQuery(
    Guid EmployeeId, string Type, DateOnly StartDate, DateOnly EndDate, bool HalfDay, int? StartHour, int? EndHour,
    Caller Caller,
    // The request being edited: its own approved days are left out so they aren't counted twice (follow-up Q2).
    Guid? ExcludeRequestId = null);

/// <summary>Days only, never rupiah (GSS03 decision 10).</summary>
public sealed record LeaveOverQuotaResult(decimal OverQuotaDays);

/// <summary>
/// How many days of a leave not yet filed would go past the employee's quota and be cut from salary, so
/// the form can warn before submitting. Same standing as filing it: whoever may file for the employee.
/// </summary>
public static class GetLeaveOverQuotaHandler
{
    public static async Task<Result<LeaveOverQuotaResult>> Handle(
        GetLeaveOverQuotaQuery query,
        IReadRepository<Employee> employees,
        IReadRepository<LeaveRequest> leaveRequests,
        IReadRepository<LeaveDeductionLine> lines,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        if (!Enum.TryParse<LeaveType>(query.Type, ignoreCase: true, out var type) || !Enum.IsDefined(type))
        {
            return new Result<LeaveOverQuotaResult>.Error(
                "leave.type", "Leave type must be Annual, Sick, Permission, or Unpaid.");
        }

        var employee = await employees.GetByIdAsync(new EmployeeId(query.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<LeaveOverQuotaResult>.NotFound("Employee was not found.");
        }

        if (!LeaveRules.CanFileFor(query.Caller, employee))
        {
            return new Result<LeaveOverQuotaResult>.Error(
                ResultErrors.Forbidden, "You cannot file leave for this employee.");
        }

        var start = LocalDate.FromDateOnly(query.StartDate);
        var end = LocalDate.FromDateOnly(query.EndDate);
        if (start > end || end.Year - start.Year > 1)
        {
            return new Result<LeaveOverQuotaResult>.Success(new LeaveOverQuotaResult(0m));
        }

        var days = await LeaveDeductionEngine.CandidateDaysAsync(
            employee, type, start, end,
            LeaveRequest.ChargePerWorkday(query.HalfDay, query.StartHour, query.EndHour, policy),
            clock.GetCurrentInstant(), policy, DisplayZone.Today(clock), leaveRequests, lines, ct,
            query.ExcludeRequestId is { } exclude ? new LeaveRequestId(exclude) : null);

        return new Result<LeaveOverQuotaResult>.Success(new LeaveOverQuotaResult(days.Sum(d => d.CutDays)));
    }
}
