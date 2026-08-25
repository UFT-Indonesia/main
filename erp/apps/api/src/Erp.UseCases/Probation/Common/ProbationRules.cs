using Erp.Core.Aggregates.Employees;
using Erp.UseCases.Common;

namespace Erp.UseCases.Probation.Common;

/// <summary>
/// Who may ask for more probation time, and who may grant it. Deliberately narrower than
/// <c>LeaveRules</c>: probation length is the Owner's call, and the request exists only so a
/// Manager who works with the person day to day can put the case.
/// </summary>
public static class ProbationRules
{
    /// <summary>
    /// A Manager may file for their own direct Staff. An Owner never files — they hold the
    /// direct edit, so a request from them would be a note to themselves.
    /// </summary>
    public static bool CanFileFor(Caller caller, Employee subject) =>
        OrgScope.IsDirectStaffOf(caller, subject);

    /// <summary>Any Owner decides. Nobody below them extends someone's probation.</summary>
    public static bool CanDecide(Caller caller) => caller.Role == EmployeeRole.Owner;

    /// <summary>Withdrawal belongs to whoever filed it.</summary>
    public static bool CanCancel(Caller caller, Guid requestedByUserId) =>
        caller.UserId == requestedByUserId;

    /// <summary>
    /// Who may see extension requests at all. An Owner sees every one; a Manager sees their own
    /// direct Staff's. Staff are not shown the file on themselves being discussed.
    /// </summary>
    public static bool CanRead(Caller caller, Employee subject) =>
        caller.Role == EmployeeRole.Owner || OrgScope.IsDirectStaffOf(caller, subject);
}
