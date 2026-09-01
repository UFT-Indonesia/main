using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Leave.Events;

/// <summary>
/// Emitted when a request becomes leave the employee is actually entitled to take —
/// from a decision, or immediately on creation for an Owner's own (nobody outranks them).
/// </summary>
/// <summary>
/// <paramref name="IsFractional"/> is true for a half day or hourly Izin — the employee is
/// expected to work the rest of the day, so attendance sync must not materialize a row for it
/// or flip the OnLeave badge on.
/// </summary>
public sealed record LeaveRequestApproved(
    Guid LeaveRequestId,
    Guid EmployeeId,
    LocalDate StartDate,
    LocalDate EndDate,
    bool IsFractional)
    : DomainEvent(LeaveRequestId, nameof(LeaveRequest), "leave.request_approved");
