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

        var removeResult = await userManager.RemoveFromRoleAsync(user, message.OldRole.ToString());
        var addResult = await userManager.AddToRoleAsync(user, message.NewRole.ToString());
        if (!removeResult.Succeeded || !addResult.Succeeded)
        {
            var errors = removeResult.Errors.Concat(addResult.Errors).Select(e => e.Description);
            throw new InvalidOperationException(
                $"Failed to sync Identity role for employee {message.EmployeeId}: {string.Join(" ", errors)}");
        }

        await refreshTokenService.RevokeAllForEmployeeAsync(message.EmployeeId, "employee_role_changed", ct);
    }
}
