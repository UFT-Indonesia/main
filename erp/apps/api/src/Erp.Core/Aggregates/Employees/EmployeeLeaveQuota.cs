using Erp.Core.Aggregates.Leave;

namespace Erp.Core.Aggregates.Employees;

/// <summary>
/// A per-employee override of one leave type's yearly entitlement, owned by
/// <see cref="Employee"/>. Permanent until cleared — deliberately *not* per-year, because a cap
/// that lapses every January fails open to uncapped, which is the dangerous direction.
/// <para>
/// <c>EntitledDays = 0</c> is a real setting ("none of this type"); the absence of a row means
/// uncapped for non-Annual types, or the computed formula for Annual.
/// </para>
/// </summary>
public sealed class EmployeeLeaveQuota
{
    // EF Core constructor.
    private EmployeeLeaveQuota() { }

    internal EmployeeLeaveQuota(LeaveType type, decimal entitledDays)
    {
        Type = type;
        EntitledDays = entitledDays;
    }

    public LeaveType Type { get; private set; }

    public decimal EntitledDays { get; private set; }

    internal void SetEntitledDays(decimal entitledDays) => EntitledDays = entitledDays;
}
