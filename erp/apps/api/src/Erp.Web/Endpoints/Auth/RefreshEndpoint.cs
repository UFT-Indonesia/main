using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using FastEndpoints;

namespace Erp.Web.Endpoints.Auth;

public sealed class RefreshEndpoint : Endpoint<EmptyRequest, AuthResponse>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAccountIdentityResolver _identityResolver;
    private readonly IHostEnvironment _environment;

    public RefreshEndpoint(
        IRefreshTokenService refreshTokenService,
        IJwtTokenService jwtTokenService,
        IAccountIdentityResolver identityResolver,
        IHostEnvironment environment)
    {
        _refreshTokenService = refreshTokenService;
        _jwtTokenService = jwtTokenService;
        _identityResolver = identityResolver;
        _environment = environment;
    }

    public override void Configure()
    {
        Post("/refresh");
        AllowAnonymous();
        Group<AuthGroup>();
        Description(d => d
            .Produces<AuthResponse>(200)
            .ProducesProblemFE(401));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
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
                var identity = await _identityResolver.ResolveAsync(success.User, ct);
                var accessToken = _jwtTokenService.CreateAccessToken(success.User, identity);
                RefreshTokenCookie.Append(HttpContext, success.PlainTextToken, success.ExpiresAtUtc, _environment);
                await SendOkAsync(new AuthResponse
                {
                    AccessToken = accessToken.AccessToken,
                    TokenType = "Bearer",
                    ExpiresAtUtc = accessToken.ExpiresAtUtc,
                    RefreshTokenExpiresAtUtc = success.ExpiresAtUtc.ToDateTimeOffset(),
                    User = AuthUserResponse.From(success.User, identity),
                }, ct);
                return;

            case RefreshTokenRotationResult.Compromised:
            case RefreshTokenRotationResult.Invalid:
                RefreshTokenCookie.Clear(HttpContext, _environment);
                await SendUnauthorizedAsync(ct);
                return;
        }
    }
}
