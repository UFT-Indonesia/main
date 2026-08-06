using System.Security.Claims;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Erp.Web.Endpoints.Auth;

[Authorize]
public sealed class ChangePasswordEndpoint : Endpoint<ChangePasswordRequest, AuthResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccountIdentityResolver _identityResolver;
    private readonly IHostEnvironment _env;

    public ChangePasswordEndpoint(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IAccountIdentityResolver identityResolver,
        IHostEnvironment env)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _identityResolver = identityResolver;
        _env = env;
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

        await _refreshTokenService.RevokeAllForUserAsync(user.Id, "password_changed", ct);

        var identity = await _identityResolver.ResolveAsync(user, ct);
        var token = _jwtTokenService.CreateAccessToken(user, identity);

        var refreshResult = await _refreshTokenService.IssueAsync(
            user,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Request.Headers.UserAgent.ToString(),
            ct);

        RefreshTokenCookie.Append(HttpContext, refreshResult, _env);

        await SendOkAsync(new AuthResponse
        {
            AccessToken = token.AccessToken,
            TokenType = "Bearer",
            ExpiresAtUtc = token.ExpiresAtUtc,
            RefreshTokenExpiresAtUtc = refreshResult.ExpiresAtUtc.ToDateTimeOffset(),
            User = AuthUserResponse.From(user, identity),
        }, ct);
    }
}
