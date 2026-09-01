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
    /// How many still-undecided requests one employee may have filed in a calendar month.
    /// Deciding one frees a slot, so this caps the queue a manager can be handed, not how much
    /// leave anyone takes — the days themselves are capped by <see cref="LeaveQuota"/>.
    /// Owners are exempt, as they are from every other leave cap.
    /// </summary>
    public const int MaxPendingRequestsPerMonth = 10;

    /// <summary>
    /// Auto-approved when there is nobody left who both outranks the subject and isn't the
    /// filer: an Owner's own leave (nothing outranks an Owner), or any leave an Owner files for
    /// someone else (an Owner could always decide it, but filing excludes the filer from
    /// deciding their own request — so without this it would be stuck, undecidable).
    /// </summary>
    public static bool IsAutoApproved(EmployeeRole subjectRole, EmployeeRole callerRole) =>
        subjectRole == EmployeeRole.Owner || callerRole == EmployeeRole.Owner;

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
    /// Who may read a request's free text — the reason and the decision note. Everyone can see
    /// *that* a colleague is away (the list is a company-wide calendar), but "Sick — surgery
    /// Thursday" is only for the employee themselves and whoever has the standing to decide it.
    /// An Owner reads everything, including another Owner's.
    /// </summary>
    public static bool CanReadDetails(Caller caller, Employee subject) =>
        caller.Role == EmployeeRole.Owner || IsSelf(caller, subject) || CanDecideFor(caller, subject);

    /// <summary>
    /// Who may read someone's running leave balance for the year. Wider than the reason — a
    /// Manager sees the whole staff's so they can plan cover, not just their own reports — but
    /// an Owner's balance is not something the people below them get to tally.
    /// </summary>
    public static bool CanReadBalance(Caller caller, Employee subject) => caller.Role switch
    {
        EmployeeRole.Owner => true,
        EmployeeRole.Manager => subject.Role != EmployeeRole.Owner,
        _ => IsSelf(caller, subject),
    };

    /// <summary>
    /// Filing a request is not authorizing it. Whoever submitted it is excluded from deciding
    /// it, so a Manager who files for their own Staff still needs an Owner to approve.
    /// </summary>
    public static bool IsRequester(Caller caller, Guid requestedByUserId) =>
        caller.UserId == requestedByUserId;

    private static bool IsSelf(Caller caller, Employee subject) => OrgScope.IsSelf(caller, subject);

    private static bool IsDirectStaffOf(Caller caller, Employee subject) =>
        OrgScope.IsDirectStaffOf(caller, subject);
}
