using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Infrastructure.Persistence.ValueConverters;

public sealed class AttendanceDayIdConverter : ValueConverter<AttendanceDayId, Guid>
{
    public AttendanceDayIdConverter()
        : base(id => id.Value, value => new AttendanceDayId(value))
    {
    }
}
