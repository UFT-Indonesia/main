namespace Erp.Core.Aggregates.Leave;

/// <summary>
/// Why a request stopped being leave. Derived from who cancelled rather than asked for:
/// cancelling your own is a withdrawal by definition, and only someone with the authority
/// to decide it can call an employee back to work.
/// </summary>
public enum LeaveCancellationReason
{
    /// <summary>The employee called their own leave off.</summary>
    WithdrawnByEmployee = 0,

    /// <summary>A manager or owner cancelled it to bring the employee in to work.</summary>
    RecalledForWork = 1,
}
