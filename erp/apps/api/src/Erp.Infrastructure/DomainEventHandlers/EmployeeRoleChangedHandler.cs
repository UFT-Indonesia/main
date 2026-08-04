using Erp.Core.Aggregates.Employees.Events;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeRoleChangedHandler
{
    /// <summary>
    /// Keeps the linked account's Identity role in sync with Employee.Role and revokes
    /// refresh tokens so the old role's access token can't be renewed.
    /// </summary>
    public static async Task Handle(
        EmployeeRoleChanged message,
        UserManager<ApplicationUser> userManager,
        IRefreshTokenService refreshTokenService,
        CancellationToken ct)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.EmployeeId == message.EmployeeId, ct);
        if (user is null)
        {
            return;
        }

        await userManager.RemoveFromRoleAsync(user, message.OldRole.ToString());
        await userManager.AddToRoleAsync(user, message.NewRole.ToString());
        await refreshTokenService.RevokeAllForEmployeeAsync(message.EmployeeId, "employee_role_changed", ct);
    }
}
