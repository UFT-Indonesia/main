using Erp.SharedKernel.Identity;

namespace Erp.Core.Interfaces;

public interface IEmployeeHierarchyLookup
{
    Task AcquireHierarchyLockAsync(CancellationToken ct);

    Task<IReadOnlyList<EmployeeId>> GetAncestorsAsync(EmployeeId employeeId, CancellationToken ct);
}
