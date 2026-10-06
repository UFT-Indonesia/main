namespace Erp.Core.Aggregates.Overtime;

/// <summary>
/// Pending → Approved | Rejected | Expired (period closed undecided); Pending/Approved → Cancelled.
/// Pending, Approved and Expired assignments all own their punches — see <see cref="OvertimeCalculator.OwnedPunches"/>.
/// </summary>
public enum OvertimeStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3,
    Expired = 4,
}
