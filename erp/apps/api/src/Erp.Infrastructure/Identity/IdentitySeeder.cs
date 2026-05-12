using Erp.Core.Aggregates.Employees;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Erp.Infrastructure.Identity;

public sealed class IdentitySeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IdentitySeedOptions _options;

    public IdentitySeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentitySeedOptions> options)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _options = options.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRolesAsync();

        if (await _userManager.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        if (!_options.HasOwnerCredentials)
        {
            throw new InvalidOperationException("Initial owner seed configuration is required when no users exist.");
        }

        var email = _options.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = _options.FullName.Trim(),
            EmailConfirmed = true,
        };

        var createResult = await _userManager.CreateAsync(user, _options.Password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to seed initial owner: {FormatErrors(createResult)}");
        }

        var roleResult = await _userManager.AddToRoleAsync(user, EmployeeRole.Owner.ToString());
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to assign owner role to initial owner: {FormatErrors(roleResult)}");
        }
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var role in Enum.GetNames<EmployeeRole>())
        {
            if (await _roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create role '{role}': {FormatErrors(result)}");
            }
        }
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
