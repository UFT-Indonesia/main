using System.Linq.Expressions;
using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Employees.Common;

internal sealed class EmployeeAuditLogListSpec : Specification<EmployeeAuditLog>
{
    public EmployeeAuditLogListSpec(
        int page, int pageSize, IReadOnlyList<Expression<Func<EmployeeAuditLog, bool>>> filters)
    {
        FilterApplier.ApplyTo(Query, filters);
        Query.OrderByDescending(log => log.OccurredAtUtc);
        Query.AsNoTracking();
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}

internal sealed class EmployeeAuditLogCountSpec : Specification<EmployeeAuditLog>
{
    public EmployeeAuditLogCountSpec(IReadOnlyList<Expression<Func<EmployeeAuditLog, bool>>> filters)
    {
        FilterApplier.ApplyTo(Query, filters);
        Query.AsNoTracking();
    }
}

/// <summary>All matching rows, unpaginated — caller must cap via a prior CountAsync check.</summary>
internal sealed class EmployeeAuditLogExportSpec : Specification<EmployeeAuditLog>
{
    public EmployeeAuditLogExportSpec(IReadOnlyList<Expression<Func<EmployeeAuditLog, bool>>> filters)
    {
        FilterApplier.ApplyTo(Query, filters);
        Query.OrderByDescending(log => log.OccurredAtUtc);
        Query.AsNoTracking();
    }
}

internal sealed class EmployeeNamesByIdSpec : Specification<Employee>
{
    public EmployeeNamesByIdSpec(IReadOnlyCollection<EmployeeId> ids)
    {
        Query.Where(employee => ids.Contains(employee.Id));
        Query.AsNoTracking();
    }
}
