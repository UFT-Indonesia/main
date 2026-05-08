using NodaTime;

namespace Erp.SharedKernel.Domain;

public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredAt = SystemClock.Instance.GetCurrentInstant();
    }

    public Guid EventId { get; init; }

    public Instant OccurredAt { get; init; }
}
