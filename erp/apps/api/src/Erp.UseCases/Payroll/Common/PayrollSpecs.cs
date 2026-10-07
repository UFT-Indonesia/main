using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Payroll.Common;

/// <summary>One employee's salary rows, tracked — the salary-change handler edits them.</summary>
public sealed class EmployeeSalaryHistorySpec : Specification<EmployeeSalaryHistory>
{
    public EmployeeSalaryHistorySpec(Guid employeeId) => Query.Where(h => h.EmployeeId == employeeId);
}

internal sealed class SalaryHistoryOfEmployeesSpec : Specification<EmployeeSalaryHistory>
{
    public SalaryHistoryOfEmployeesSpec(IReadOnlyCollection<Guid> employeeIds)
    {
        Query.Where(h => employeeIds.Contains(h.EmployeeId));
        Query.AsNoTracking();
    }
}

/// <summary>Every closed month. Small: one row a month.</summary>
internal sealed class ClosedDeductionMonthsSpec : Specification<LeaveDeductionMonth>
{
    public ClosedDeductionMonthsSpec()
    {
        Query.OrderByDescending(m => m.Month);
        Query.AsNoTracking();
    }
}

internal sealed class DeductionMonthSpec : SingleResultSpecification<LeaveDeductionMonth>
{
    public DeductionMonthSpec(LocalDate month)
    {
        Query.Where(m => m.Month == month);
        Query.AsNoTracking();
    }
}

internal sealed class DeductionLinesOfMonthSpec : Specification<LeaveDeductionLine>
{
    public DeductionLinesOfMonthSpec(LocalDate month, bool tracked = false)
    {
        Query.Where(l => l.Month == month);
        if (!tracked)
        {
            Query.AsNoTracking();
        }
    }
}

/// <summary>Frozen labels for the given employees within a span of calendar years.</summary>
internal sealed class DeductionLinesForEmployeesSpec : Specification<LeaveDeductionLine>
{
    public DeductionLinesForEmployeesSpec(IReadOnlyCollection<Guid> employeeIds, int fromYear, int toYear)
    {
        var from = new LocalDate(fromYear, 1, 1);
        var to = new LocalDate(toYear, 12, 31);
        Query.Where(l => employeeIds.Contains(l.EmployeeId) && l.Date >= from && l.Date <= to);
        Query.AsNoTracking();
    }
}

/// <summary>Approved leave overlapping a calendar year, for everyone — the cap is consumed per year.</summary>
internal sealed class ApprovedLeaveInYearSpec : Specification<LeaveRequest>
{
    public ApprovedLeaveInYearSpec(int year)
    {
        var start = new LocalDate(year, 1, 1);
        var end = new LocalDate(year, 12, 31);
        Query.Where(r => r.Status == LeaveRequestStatus.Approved && r.StartDate <= end && start <= r.EndDate);
        Query.AsNoTracking();
    }
}

internal sealed class PendingLeaveOverlappingSpec : Specification<LeaveRequest>
{
    public PendingLeaveOverlappingSpec(LocalDate start, LocalDate end)
    {
        Query.Where(r => r.Status == LeaveRequestStatus.Pending && r.StartDate <= end && start <= r.EndDate);
        Query.Include(r => r.Employee);
        Query.AsNoTracking();
    }
}

internal sealed class LeaveRequestsByIdsSpec : Specification<LeaveRequest>
{
    public LeaveRequestsByIdsSpec(IReadOnlyCollection<LeaveRequestId> ids)
    {
        Query.Where(r => ids.Contains(r.Id));
        Query.AsNoTracking();
    }
}

internal sealed class EmployeesByIdsSpec : Specification<Employee>
{
    public EmployeesByIdsSpec(IReadOnlyCollection<EmployeeId> ids)
    {
        Query.Where(e => ids.Contains(e.Id));
        Query.AsNoTracking();
    }
}
