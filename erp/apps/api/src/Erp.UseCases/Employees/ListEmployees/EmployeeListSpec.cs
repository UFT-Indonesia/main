using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;

namespace Erp.UseCases.Employees.ListEmployees;

internal sealed class EmployeeListSpec : Specification<Employee>
{
    public EmployeeListSpec(
        int page,
        int pageSize,
        string? search,
        EmployeeRole? role,
        EmployeeStatus? status)
    {
        ApplyFilters(Query, search, role, status);
        Query.OrderBy(e => e.FullName);
        Query.AsNoTracking();
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }

    internal static void ApplyFilters(
        ISpecificationBuilder<Employee> query,
        string? search,
        EmployeeRole? role,
        EmployeeStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim().ToLowerInvariant();
            query.Where(e => e.FullName.ToLower().Contains(needle)
                || e.Nik.Value.Contains(needle));
        }

        if (role.HasValue)
        {
            query.Where(e => e.Role == role.Value);
        }

        if (status.HasValue)
        {
            query.Where(e => e.Status == status.Value);
        }
    }
}

internal sealed class EmployeeListCountSpec : Specification<Employee>
{
    public EmployeeListCountSpec(string? search, EmployeeRole? role, EmployeeStatus? status)
    {
        EmployeeListSpec.ApplyFilters(Query, search, role, status);
        Query.AsNoTracking();
    }
}
