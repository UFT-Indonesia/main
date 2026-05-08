using NodaTime;

namespace Erp.SharedKernel.Domain;

public interface IDateTimeProvider
{
    Instant Now { get; }

    LocalDate Today { get; }
}
