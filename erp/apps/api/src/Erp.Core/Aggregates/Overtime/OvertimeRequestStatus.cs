namespace Erp.Core.Aggregates.Overtime;

/// <summary>Lifecycle shared by correction requests and rapel: Pending → Approved | Rejected | Expired.</summary>
public enum OvertimeRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Expired = 3,
}
