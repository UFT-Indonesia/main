using NodaTime;

namespace Erp.SharedKernel.Domain;

/// <summary>
/// Domain event envelope. The metadata here describes the EVENT itself
/// (when it was emitted, what it represents, which aggregate it belongs to),
/// not the business fact carried in the payload — those live as explicit
/// fields on each concrete event (e.g. <c>PunchedAtUtc</c>).
///
/// The shape is intentionally compatible with CloudEvents-style consumers
/// so it can be projected to outbox / webhook payloads later.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Unique id of this event instance (idempotency key for consumers).</summary>
    Guid EventId { get; }

    /// <summary>UTC instant the event was emitted by the domain (NOT when the business fact occurred).</summary>
    Instant RaisedAt { get; }

    /// <summary>Stable wire name, e.g. <c>employee.created</c>, <c>attendance.recorded</c>.</summary>
    string EventType { get; }

    /// <summary>Schema version for this event type. Bump on breaking payload changes.</summary>
    int EventVersion { get; }

    /// <summary>Id of the aggregate root that emitted this event.</summary>
    Guid AggregateId { get; }

    /// <summary>Aggregate root type name, e.g. <c>Employee</c>, <c>AttendanceLog</c>.</summary>
    string AggregateType { get; }
}
