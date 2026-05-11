using Microsoft.AspNetCore.Identity;

namespace Erp.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public Guid? EmployeeId { get; set; }
}
