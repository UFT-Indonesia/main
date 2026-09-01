using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using NodaTime;

namespace Erp.UseCases.Leave.GetLeaveBalance;

/// <summary>
/// One employee's standing across all four leave types, for the leave dialog and the employee
/// detail page. Read-only and derived — nothing here is stored, so it cannot go stale.
/// </summary>
public static class GetLeaveBalanceHandler
{
    public static async Task<Result<LeaveBalanceResult>> Handle(
        GetLeaveBalanceQuery query,
        IReadRepository<Employee> employees,
        IReadRepository<LeaveRequest> leaveRequests,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        var employee = await employees.GetByIdAsync(new EmployeeId(query.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<LeaveBalanceResult>.NotFound("Employee was not found.");
        }

        if (!LeaveRules.CanReadBalance(query.Caller, employee))
        {
            return new Result<LeaveBalanceResult>.Error(
                ResultErrors.Forbidden, "You cannot read this employee's leave balance.");
        }

        var today = DisplayZone.Today(clock);
        var year = query.Year ?? today.Year;

        var approved = await leaveRequests.ListAsync(
            new ApprovedLeaveForYearSpec([employee.Id], year), ct);

        return new Result<LeaveBalanceResult>.Success(new LeaveBalanceResult
        {
            EmployeeId = employee.Id.Value,
            EmployeeFullName = employee.FullName,
            Year = year,
            OnProbation = employee.IsOnProbation(today),
            ProbationEndsOn = employee.ProbationEndsOn?.ToDateOnly(),
            Quotas = Enum.GetValues<LeaveType>()
                .Select(type => LeaveQuotaResult.For(employee, type, year, today, approved, policy))
                .ToList(),
        });
    }
}
