using System.Security.Claims;
using Erp.Infrastructure.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Erp.Web.Endpoints.Auth;

[Authorize]
public sealed class ChangePasswordEndpoint : Endpoint<ChangePasswordRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ChangePasswordEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Post("/change-password");
        Group<AuthGroup>();
    }

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
        {
            ThrowError("Current and new password are required.", 400);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
        {
            ThrowError(string.Join(" ", result.Errors.Select(e => e.Description)), 400);
        }

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);
        }

        // Client should hit /api/auth/refresh next to get a token without the pwd_change claim.
        await SendNoContentAsync(ct);
    }
}
