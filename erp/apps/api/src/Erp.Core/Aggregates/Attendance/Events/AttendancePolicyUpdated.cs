using Erp.SharedKernel.Domain;

namespace Erp.Core.Aggregates.Attendance.Events;

/// <summary>
/// Emitted whenever the global attendance shift/grace-period policy changes.
/// Triggers a background recompute of every materialized AttendanceDay row.
/// </summary>
public sealed record AttendancePolicyUpdated(Guid PolicyId)
    : DomainEvent(PolicyId, nameof(AttendancePolicy), "attendance.policy_updated");
