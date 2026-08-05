using Erp.Core.Aggregates.Employees;
using Erp.UseCases.Common;

namespace Erp.UseCases.Leave.Common;

/// <summary>
/// Who may act on whose leave. Authority follows the reporting line: an approver must
/// outrank the subject, and the person who filed a request never approves it themselves.
/// </summary>
public static class LeaveRules
{
    /// <summary>
    /// An Owner's leave is a calendar label rather than a request — there is nobody above
    /// them to approve it, so it is approved the moment it is filed.
    /// </summary>
    public static bool IsAutoApproved(EmployeeRole subjectRole) => subjectRole == EmployeeRole.Owner;

    /// <summary>Owner files for anyone; Manager for themselves or their own Staff; everyone else only for themselves.</summary>
    public static bool CanFileFor(Caller caller, Employee subject) => caller.Role switch
    {
        EmployeeRole.Owner => true,
        EmployeeRole.Manager => IsSelf(caller, subject) || IsDirectStaffOf(caller, subject),
        _ => IsSelf(caller, subject),
    };

    /// <summary>
    /// Staff leave is decided by their own manager or any Owner; a Manager's leave only by an
    /// Owner. An Owner's leave is never decided — it was approved on creation.
    /// </summary>
    public static bool CanDecideFor(Caller caller, Employee subject) => subject.Role switch
    {
        EmployeeRole.Owner => false,
        EmployeeRole.Manager => caller.Role == EmployeeRole.Owner,
        _ => caller.Role == EmployeeRole.Owner || IsDirectStaffOf(caller, subject),
    };

    /// <summary>
    /// The person on leave may always withdraw or cancel their own; otherwise it takes the
    /// same authority that could have approved it.
    /// </summary>
    public static bool CanCancel(Caller caller, Employee subject) =>
        IsSelf(caller, subject) || CanDecideFor(caller, subject);

    /// <summary>
    /// Filing a request is not authorizing it. Whoever submitted it is excluded from deciding
    /// it, so a Manager who files for their own Staff still needs an Owner to approve.
    /// </summary>
    public static bool IsRequester(Caller caller, Guid requestedByUserId) =>
        caller.UserId == requestedByUserId;

    private static bool IsSelf(Caller caller, Employee subject) =>
        caller.EmployeeId.HasValue && caller.EmployeeId.Value == subject.Id;

    private static bool IsDirectStaffOf(Caller caller, Employee subject) =>
        caller.Role == EmployeeRole.Manager
        && subject.Role == EmployeeRole.Staff
        && caller.EmployeeId.HasValue
        && subject.ParentId == caller.EmployeeId.Value;
}
