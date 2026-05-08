using NodaTime;

namespace Erp.SharedKernel.Domain;

/// <summary>
/// Base record for all domain events. Concrete events pass their identity
/// via the primary constructor and add their own payload fields:
/// <code>
/// public sealed record EmployeeCreated(Guid EmployeeId, ...)
///     : DomainEvent(EmployeeId, nameof(Employee), "employee.created");
/// </code>
/// </summary>
public abstract record DomainEvent(
    Guid AggregateId,
    string AggregateType,
    string EventType,
    int EventVersion = 1) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public Instant RaisedAt { get; init; } = SystemClock.Instance.GetCurrentInstant();
}
