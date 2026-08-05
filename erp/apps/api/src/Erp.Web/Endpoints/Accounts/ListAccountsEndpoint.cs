using Erp.Infrastructure.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Erp.Web.Endpoints.Accounts;

[Authorize(Roles = "Owner,Manager")]
public sealed class ListAccountsEndpoint : EndpointWithoutRequest<ListAccountsResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountIdentityResolver _identityResolver;

    public ListAccountsEndpoint(
        UserManager<ApplicationUser> userManager,
        IAccountIdentityResolver identityResolver)
    {
        _userManager = userManager;
        _identityResolver = identityResolver;
    }

    public override void Configure()
    {
        Get("/");
        Group<AccountsGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct);

        // ponytail: one employee lookup per account — fine for a small org, batch into a
        // single join on Employees if the account list ever gets slow.
        var items = new List<AccountResponse>(users.Count);
        foreach (var user in users)
        {
            var role = (await _identityResolver.ResolveAsync(user, ct)).Role;

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
