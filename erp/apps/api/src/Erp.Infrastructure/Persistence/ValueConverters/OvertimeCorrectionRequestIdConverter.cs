using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Infrastructure.Persistence.ValueConverters;

public sealed class OvertimeCorrectionRequestIdConverter : ValueConverter<OvertimeCorrectionRequestId, Guid>
{
    public OvertimeCorrectionRequestIdConverter()
        : base(id => id.Value, value => new OvertimeCorrectionRequestId(value))
    {
    }
}
