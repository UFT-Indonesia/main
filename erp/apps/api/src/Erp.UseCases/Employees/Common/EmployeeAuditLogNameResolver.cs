using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;

namespace Erp.UseCases.Employees.Common;

/// <summary>Bulk id→name lookup for the audited employee shown on each audit-log row.</summary>
internal static class EmployeeAuditLogNameResolver
{
    public static async Task<IReadOnlyDictionary<EmployeeId, string>> ResolveAsync(
        IReadRepository<Employee> employees, IEnumerable<EmployeeId> employeeIds, CancellationToken ct)
    {
        var ids = employeeIds.Distinct().ToList();
        var matches = await employees.ListAsync(new EmployeeNamesByIdSpec(ids), ct);
        return matches.ToDictionary(employee => employee.Id, employee => employee.FullName);
    }
}
