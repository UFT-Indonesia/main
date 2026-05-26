using Erp.Infrastructure.Authentication;
using FastEndpoints;

namespace Erp.Web.Endpoints.Auth;

public sealed class LogoutEndpoint : EndpointWithoutRequest
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IHostEnvironment _environment;

    public LogoutEndpoint(IRefreshTokenService refreshTokenService, IHostEnvironment environment)
    {
        _refreshTokenService = refreshTokenService;
        _environment = environment;
    }

    public override void Configure()
    {
        Post("/logout");
        AllowAnonymous();
        Group<AuthGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.Request.Cookies.TryGetValue(RefreshTokenCookie.Name, out var cookieToken))
        {
            await _refreshTokenService.RevokeFamilyForTokenAsync(cookieToken, "logout", ct);
        }

        RefreshTokenCookie.Clear(HttpContext, _environment);
        await SendNoContentAsync(ct);
    }
}
