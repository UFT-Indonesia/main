using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Erp.Web.Endpoints.Accounts;

[Authorize(Roles = "Owner,Manager")]
public sealed class ResetAccountPasswordEndpoint : Endpoint<AccountIdRequest, ResetAccountPasswordResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenService _refreshTokenService;

    public ResetAccountPasswordEndpoint(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenService refreshTokenService)
    {
        _userManager = userManager;
        _refreshTokenService = refreshTokenService;
    }

    public override void Configure()
    {
        Post("/{id}/reset-password");
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

        if (!AccountRules.CanManage(User, AccountRules.RoleFromNames(await _userManager.GetRolesAsync(user))))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        var tempPassword = TempPassword.Generate();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, tempPassword);
        if (!result.Succeeded)
        {
            ThrowError(string.Join(" ", result.Errors.Select(e => e.Description)), 400);
        }

        user.MustChangePassword = true;
        await _userManager.UpdateAsync(user);
        await _refreshTokenService.RevokeAllForUserAsync(user.Id, "password_reset", ct);

        await SendOkAsync(new ResetAccountPasswordResponse { TempPassword = tempPassword }, ct);
    }
}
