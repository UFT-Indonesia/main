using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Infrastructure.Persistence.ValueConverters;

public sealed class GajiPremiPeriodIdConverter : ValueConverter<GajiPremiPeriodId, Guid>
{
    public GajiPremiPeriodIdConverter()
        : base(id => id.Value, value => new GajiPremiPeriodId(value))
    {
    }
}
