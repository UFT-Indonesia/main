using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Infrastructure.Persistence.ValueConverters;

public sealed class OvertimeAssignmentIdConverter : ValueConverter<OvertimeAssignmentId, Guid>
{
    public OvertimeAssignmentIdConverter()
        : base(id => id.Value, value => new OvertimeAssignmentId(value))
    {
    }
}
