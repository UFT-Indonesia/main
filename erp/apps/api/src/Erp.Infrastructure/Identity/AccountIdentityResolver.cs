using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Persistence;
using Erp.SharedKernel.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Identity;

/// <summary>The authoritative role for an account, plus the employee it speaks for.</summary>
public readonly record struct AccountIdentity(EmployeeRole Role, Guid? EmployeeId);

public interface IAccountIdentityResolver
{
    Task<AccountIdentity> ResolveAsync(ApplicationUser user, CancellationToken ct);
}

/// <summary>
/// <see cref="Employee.Role"/> is the single source of truth for authorization — the
/// Identity role rows are only consulted for legacy accounts that predate employee
/// linking, so the two copies can no longer drift into disagreeing.
/// </summary>
public sealed class AccountIdentityResolver : IAccountIdentityResolver
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountIdentityResolver(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<AccountIdentity> ResolveAsync(ApplicationUser user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.EmployeeId is { } employeeId)
        {
            var typedId = new EmployeeId(employeeId);
            var role = await _db.Employees.AsNoTracking()
                .Where(employee => employee.Id == typedId)
                .Select(employee => (EmployeeRole?)employee.Role)
                .FirstOrDefaultAsync(ct);

            if (role.HasValue)
            {
                return new AccountIdentity(role.Value, employeeId);
            }
        }

        var storedRoles = await _userManager.GetRolesAsync(user);
        return new AccountIdentity(EmployeeRoleNames.MostPrivileged(storedRoles), user.EmployeeId);
    }
}

public static class EmployeeRoleNames
{
    /// <summary>Most privileged role wins when several are present (Owner &lt; Manager &lt; Staff by enum value).</summary>
    public static EmployeeRole MostPrivileged(IEnumerable<string> roles) =>
        roles
            .Select(role => Enum.TryParse<EmployeeRole>(role, out var parsed) ? parsed : (EmployeeRole?)null)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .DefaultIfEmpty(EmployeeRole.Staff)
            .Min();
}
