using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Infrastructure.Persistence.ValueConverters;

public sealed class AttendanceDeviceIdConverter : ValueConverter<AttendanceDeviceId, Guid>
{
    public AttendanceDeviceIdConverter()
        : base(id => id.Value, value => new AttendanceDeviceId(value))
    {
    }
}
