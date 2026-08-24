using Erp.SharedKernel.Domain;

namespace Erp.Core.Aggregates.Leave.Events;

/// <summary>
/// Emitted only when leave that was already <see cref="LeaveRequestStatus.Approved"/> is
/// called off — a withdrawn Pending request never reached attendance, so there is nothing
/// downstream to undo. The covered days are found by the foreign key, not by re-walking the
/// range, so the dates are not part of the payload.
/// </summary>
public sealed record LeaveRequestCancelled(
    Guid LeaveRequestId,
    Guid EmployeeId)
    : DomainEvent(LeaveRequestId, nameof(LeaveRequest), "leave.request_cancelled");
