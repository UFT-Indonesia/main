using NodaTime;

namespace Erp.SharedKernel.Domain;

public interface IDomainEvent
{
    Guid EventId { get; }

    Instant OccurredAt { get; }
}
