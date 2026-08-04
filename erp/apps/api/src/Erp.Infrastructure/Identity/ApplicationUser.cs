using Microsoft.AspNetCore.Identity;

namespace Erp.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public Guid? EmployeeId { get; set; }

    /// <summary>
    /// Set on provisioning and admin resets; the user must change their
    /// temporary password before accessing anything beyond /api/auth.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
