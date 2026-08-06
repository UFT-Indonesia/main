using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Persistence;
using Erp.SharedKernel.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Erp.Infrastructure.Identity;

public sealed class IdentitySeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IdentitySeedOptions _options;

    public IdentitySeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        AppDbContext db,
        IClock clock,
        IOptions<IdentitySeedOptions> options)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _clock = clock;
        _options = options.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRolesAsync();

        var owner = await EnsureOwnerAccountAsync(cancellationToken);
        if (owner is null)
        {
            return;
        }

        // Authorization now reads Employee.Role, so the owner needs a real employee record
        // — without one it cannot hold leave or be resolved as an approver.
        await EnsureOwnerEmployeeAsync(owner, cancellationToken);
    }

    /// <summary>Returns the owner account, creating it on a fresh database. Null when nothing is configured.</summary>
    private async Task<ApplicationUser?> EnsureOwnerAccountAsync(CancellationToken ct)
    {
        if (await _userManager.Users.AnyAsync(ct))
        {
            // Existing deployment: only the configured owner is a candidate for backfill.
            return string.IsNullOrWhiteSpace(_options.Email)
                ? null
                : await _userManager.FindByNameAsync(_options.Email.Trim());
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

        return user;
    }

    private async Task EnsureOwnerEmployeeAsync(ApplicationUser owner, CancellationToken ct)
    {
        if (owner.EmployeeId.HasValue)
        {
            var linkedId = new EmployeeId(owner.EmployeeId.Value);
            if (await _db.Employees.AnyAsync(employee => employee.Id == linkedId, ct))
            {
                return;
            }
        }

        var employee = Employee.Create(
            string.IsNullOrWhiteSpace(owner.FullName) ? _options.FullName.Trim() : owner.FullName,
            Nik.Create(_options.Nik),
            Money.Idr(_options.MonthlyWage),
            _clock.GetCurrentInstant().InUtc().Date,
            EmployeeRole.Owner);

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        owner.EmployeeId = employee.Id.Value;
        var linkResult = await _userManager.UpdateAsync(owner);
        if (!linkResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to link the owner account to its employee record: {FormatErrors(linkResult)}");
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
