using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Infrastructure.Persistence.ValueConverters;

public sealed class EmployeeIdConverter : ValueConverter<EmployeeId, Guid>
{
    public EmployeeIdConverter()
        : base(id => id.Value, value => new EmployeeId(value))
    {
    }
}
