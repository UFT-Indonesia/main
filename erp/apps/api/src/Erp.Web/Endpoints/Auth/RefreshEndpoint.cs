using Erp.Infrastructure.Authentication;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

namespace Erp.Web.Endpoints.Auth;

public sealed class RefreshEndpoint : EndpointWithoutRequest
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IHostEnvironment _environment;

    public RefreshEndpoint(
        IRefreshTokenService refreshTokenService,
        IJwtTokenService jwtTokenService,
        IHostEnvironment environment)
    {
        _refreshTokenService = refreshTokenService;
        _jwtTokenService = jwtTokenService;
        _environment = environment;
    }

    public override void Configure()
    {
        Post("/refresh");
        AllowAnonymous();
        Group<AuthGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!HttpContext.Request.Cookies.TryGetValue(RefreshTokenCookie.Name, out var cookieToken)
            || string.IsNullOrWhiteSpace(cookieToken))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _refreshTokenService.RotateAsync(
            cookieToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Request.Headers.UserAgent.ToString(),
            ct);

        switch (result)
        {
            case RefreshTokenRotationResult.Success success:
                var roles = await HttpContext.Resolve<UserManager<Infrastructure.Identity.ApplicationUser>>()
                    .GetRolesAsync(success.User);
                var accessToken = _jwtTokenService.CreateAccessToken(success.User, roles);
                RefreshTokenCookie.Append(HttpContext, success.PlainTextToken, success.ExpiresAtUtc, _environment);
                await SendOkAsync(new AuthResponse
                {
                    AccessToken = accessToken.AccessToken,
                    TokenType = "Bearer",
                    ExpiresAtUtc = accessToken.ExpiresAtUtc,
                    RefreshTokenExpiresAtUtc = success.ExpiresAtUtc.ToDateTimeOffset(),
                    User = new AuthUserResponse
                    {
                        Id = success.User.Id,
                        Email = success.User.Email ?? string.Empty,
                        FullName = success.User.FullName,
                        EmployeeId = success.User.EmployeeId,
                        Roles = roles,
                    },
                }, ct);
                return;

            case RefreshTokenRotationResult.Compromised compromised:
                RefreshTokenCookie.Clear(HttpContext, _environment);
                await SendAsync(new AuthErrorResponse { Message = compromised.Message }, 401, ct);
                return;

            case RefreshTokenRotationResult.Invalid invalid:
                RefreshTokenCookie.Clear(HttpContext, _environment);
                await SendAsync(new AuthErrorResponse { Message = invalid.Message }, 401, ct);
                return;
        }
    }
}
