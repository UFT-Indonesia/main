using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;

namespace Erp.UseCases.Employees.Common;

/// <summary>
/// Employees still reporting to the given parent. ParentId is an authorization boundary —
/// leave approval and attendance corrections route through it — so a manager cannot be
/// terminated while people still hang off them, or their reports would silently lose their
/// approver.
/// </summary>
internal sealed class ActiveDirectReportsSpec : Specification<Employee>
{
    public ActiveDirectReportsSpec(EmployeeId parentId)
    {
        Query.Where(employee => employee.ParentId == parentId
                                && employee.Status != EmployeeStatus.Terminated);
        Query.OrderBy(employee => employee.FullName);
        Query.AsNoTracking();
    }
}
