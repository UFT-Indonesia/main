using System.Security.Claims;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Erp.Web.Endpoints.Accounts;

[Authorize(Roles = "Owner,Manager")]
public sealed class DeleteAccountEndpoint : Endpoint<AccountIdRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccountIdentityResolver _identityResolver;

    public DeleteAccountEndpoint(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenService refreshTokenService,
        IAccountIdentityResolver identityResolver)
    {
        _userManager = userManager;
        _refreshTokenService = refreshTokenService;
        _identityResolver = identityResolver;
    }

    public override void Configure()
    {
        Delete("/{id}");
        Group<AccountsGroup>();
    }

    public override async Task HandleAsync(AccountIdRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(req.Id.ToString());
        if (user is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var identity = await _identityResolver.ResolveAsync(user, ct);
        if (!AccountRules.CanManage(User, identity.Role))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        if (user.Id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            ThrowError("You cannot delete your own account.", 400);
        }

        await _refreshTokenService.RevokeAllForUserAsync(user.Id, "account_deleted", ct);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            ThrowError(string.Join(" ", result.Errors.Select(e => e.Description)), 400);
        }

        await SendNoContentAsync(ct);
    }
}
