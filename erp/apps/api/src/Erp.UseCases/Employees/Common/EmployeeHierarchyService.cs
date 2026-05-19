using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;

namespace Erp.UseCases.Employees.Common;

public static class EmployeeHierarchyService
{
    public static async Task<IReadOnlyList<EmployeeId>> ResolveAncestorsForParentAsync(
        EmployeeId? parentId,
        IEmployeeHierarchyLookup lookup,
        CancellationToken ct)
    {
        if (!parentId.HasValue)
        {
            return Array.Empty<EmployeeId>();
        }

        await lookup.AcquireHierarchyLockAsync(ct);
        var ancestors = await lookup.GetAncestorsAsync(parentId.Value, ct);

        if (ancestors.Count >= EmployeeHierarchyPolicy.MaxAncestryWalk)
        {
            throw new DomainException(
                "employee.hierarchy_corrupted",
                "Ancestry chain exceeds safety limit; possible cycle in data.");
        }

        return ancestors;
    }
}
