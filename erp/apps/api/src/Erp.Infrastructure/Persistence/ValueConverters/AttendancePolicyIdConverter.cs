using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Infrastructure.Persistence.ValueConverters;

public sealed class AttendancePolicyIdConverter : ValueConverter<AttendancePolicyId, Guid>
{
    public AttendancePolicyIdConverter()
        : base(id => id.Value, value => new AttendancePolicyId(value))
    {
    }
}
