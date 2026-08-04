using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Identity;
using Erp.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Erp.Web.Endpoints.Accounts;

[Authorize(Roles = "Owner,Manager")]
public sealed class ListAccountsEndpoint : EndpointWithoutRequest<ListAccountsResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public ListAccountsEndpoint(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public override void Configure()
    {
        Get("/");
        Group<AccountsGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct);

        // ponytail: one GetRolesAsync per account — fine for a small org, batch via a
        // join on AuthUserRoles if the account list ever gets slow.
        var items = new List<AccountResponse>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = AccountRules.RoleFromNames(roles);

            if (!AccountRules.CanManage(User, role))
            {
                continue; // Managers only see accounts they can manage.
            }

            items.Add(new AccountResponse
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email,
                FullName = user.FullName,
                EmployeeId = user.EmployeeId,
                Role = role.ToString(),
                IsEnabled = user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow,
                MustChangePassword = user.MustChangePassword,
            });
        }

        await SendOkAsync(new ListAccountsResponse { Items = items }, ct);
    }
}
