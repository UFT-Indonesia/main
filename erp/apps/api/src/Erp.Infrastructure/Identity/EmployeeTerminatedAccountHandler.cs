using Erp.Core.Aggregates.Employees.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Identity;

/// <summary>
/// Locks the terminated employee's account. Refresh tokens are revoked separately by
/// <see cref="Authentication.EmployeeTerminatedRefreshTokenHandler"/>.
/// </summary>
public static class EmployeeTerminatedAccountHandler
{
    public static async Task Handle(
        EmployeeTerminated message,
        UserManager<ApplicationUser> userManager,
        CancellationToken ct)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.EmployeeId == message.EmployeeId, ct);
        if (user is null)
        {
            return;
        }

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
    }
}
