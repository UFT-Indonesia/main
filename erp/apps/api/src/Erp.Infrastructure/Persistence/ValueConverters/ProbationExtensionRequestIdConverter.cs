using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Infrastructure.Persistence.ValueConverters;

public sealed class ProbationExtensionRequestIdConverter : ValueConverter<ProbationExtensionRequestId, Guid>
{
    public ProbationExtensionRequestIdConverter()
        : base(id => id.Value, value => new ProbationExtensionRequestId(value))
    {
    }
}
