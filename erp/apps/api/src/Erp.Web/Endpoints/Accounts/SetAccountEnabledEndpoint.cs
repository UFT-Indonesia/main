using System.Security.Claims;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Erp.Web.Endpoints.Accounts;

[Authorize(Roles = "Owner,Manager")]
public sealed class SetAccountEnabledEndpoint : Endpoint<SetAccountEnabledRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenService _refreshTokenService;

    public SetAccountEnabledEndpoint(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenService refreshTokenService)
    {
        _userManager = userManager;
        _refreshTokenService = refreshTokenService;
    }

    public override void Configure()
    {
        Patch("/{id}/enabled");
        Group<AccountsGroup>();
    }

    public override async Task HandleAsync(SetAccountEnabledRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(req.Id.ToString());
        if (user is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (!AccountRules.CanManage(User, AccountRules.RoleFromNames(await _userManager.GetRolesAsync(user))))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        if (!req.Enabled && user.Id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            ThrowError("You cannot disable your own account.", 400);
        }

        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, req.Enabled ? null : DateTimeOffset.MaxValue);

        if (req.Enabled)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }
        else
        {
            await _refreshTokenService.RevokeAllForUserAsync(user.Id, "account_disabled", ct);
        }

        await SendNoContentAsync(ct);
    }
}
