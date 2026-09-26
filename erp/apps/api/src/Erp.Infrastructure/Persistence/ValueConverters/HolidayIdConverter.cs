using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Infrastructure.Persistence.ValueConverters;

public sealed class HolidayIdConverter : ValueConverter<HolidayId, Guid>
{
    public HolidayIdConverter()
        : base(id => id.Value, value => new HolidayId(value))
    {
    }
}
