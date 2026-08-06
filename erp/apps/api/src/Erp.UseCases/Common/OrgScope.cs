using Erp.Core.Aggregates.Employees;

namespace Erp.UseCases.Common;

/// <summary>
/// The reporting-line predicates every vertical's authority rules are built from. Kept in
/// one place so leave and attendance cannot drift into disagreeing about what "my staff"
/// means.
/// </summary>
public static class OrgScope
{
    public static bool IsSelf(Caller caller, Employee subject) =>
        caller.EmployeeId.HasValue && caller.EmployeeId.Value == subject.Id;

    /// <summary>
    /// True only for a Manager's own direct Staff. Depth is capped at Owner → Manager → Staff,
    /// so a Manager's reports are exactly their direct children.
    /// </summary>
    public static bool IsDirectStaffOf(Caller caller, Employee subject) =>
        caller.Role == EmployeeRole.Manager
        && subject.Role == EmployeeRole.Staff
        && caller.EmployeeId.HasValue
        && subject.ParentId == caller.EmployeeId.Value;
}
