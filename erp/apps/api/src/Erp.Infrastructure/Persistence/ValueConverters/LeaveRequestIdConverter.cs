using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Infrastructure.Persistence.ValueConverters;

public sealed class LeaveRequestIdConverter : ValueConverter<LeaveRequestId, Guid>
{
    public LeaveRequestIdConverter()
        : base(id => id.Value, value => new LeaveRequestId(value))
    {
    }
}
