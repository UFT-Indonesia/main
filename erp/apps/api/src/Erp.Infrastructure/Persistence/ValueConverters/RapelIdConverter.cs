using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Infrastructure.Persistence.ValueConverters;

public sealed class RapelIdConverter : ValueConverter<RapelId, Guid>
{
    public RapelIdConverter()
        : base(id => id.Value, value => new RapelId(value))
    {
    }
}
