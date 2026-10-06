using Erp.Core.Aggregates.Employees;
using Erp.UseCases.Common;

namespace Erp.UseCases.Overtime.Common;

/// <summary>Who may assign, decide and see overtime. Reuses the reporting-line predicates leave is built from.</summary>
public static class OvertimeRules
{
    /// <summary>Owner → anyone but an Owner (Managers included); Manager → their own direct Staff.</summary>
    public static bool CanAssign(Caller caller, Employee subject) =>
        subject.Role != EmployeeRole.Owner
        && (caller.Role == EmployeeRole.Owner || OrgScope.IsDirectStaffOf(caller, subject));

    /// <summary>Only an Owner approves or rejects — a Manager's own assignment can never approve itself.</summary>
    public static bool CanDecide(Caller caller) => caller.Role == EmployeeRole.Owner;

    public static bool CanRead(Caller caller, Employee subject) =>
        caller.Role == EmployeeRole.Owner || OrgScope.IsSelf(caller, subject) || OrgScope.IsDirectStaffOf(caller, subject);

    /// <summary>
    /// A UI rule, not a secret (GSS08): a Manager sees windows, punches and tiers, and could
    /// work the amount out, so the figure is simply not sent to them.
    /// </summary>
    public static bool CanSeePay(Caller caller, Employee subject) =>
        caller.Role == EmployeeRole.Owner || OrgScope.IsSelf(caller, subject);
}
