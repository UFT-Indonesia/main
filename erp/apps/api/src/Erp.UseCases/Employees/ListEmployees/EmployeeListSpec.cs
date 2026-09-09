using System.Linq.Expressions;
using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Employees.ListEmployees;

internal sealed class EmployeeListSpec : Specification<Employee>
{
    public EmployeeListSpec(int page, int pageSize, IReadOnlyList<Expression<Func<Employee, bool>>> filters)
    {
        FilterApplier.ApplyTo(Query, filters);
        Query.OrderBy(employee => employee.FullName);
        Query.AsNoTracking();
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}

internal sealed class EmployeeListCountSpec : Specification<Employee>
{
    public EmployeeListCountSpec(IReadOnlyList<Expression<Func<Employee, bool>>> filters)
    {
        FilterApplier.ApplyTo(Query, filters);
        Query.AsNoTracking();
    }
}
