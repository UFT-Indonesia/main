using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Leave.Events;

/// <summary>
/// Emitted when a request becomes leave the employee is actually entitled to take —
/// from a decision, or immediately on creation for an Owner's own (nobody outranks them).
/// </summary>
public sealed record LeaveRequestApproved(
    Guid LeaveRequestId,
    Guid EmployeeId,
    LocalDate StartDate,
    LocalDate EndDate)
    : DomainEvent(LeaveRequestId, nameof(LeaveRequest), "leave.request_approved");
