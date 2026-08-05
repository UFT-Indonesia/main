using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

namespace Erp.Web.Endpoints.Auth;

public sealed class LoginEndpoint : Endpoint<LoginRequest, AuthResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccountIdentityResolver _identityResolver;
    private readonly IHostEnvironment _env;

    public LoginEndpoint(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IAccountIdentityResolver identityResolver,
        IHostEnvironment env)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _identityResolver = identityResolver;
        _env = env;
    }

    public override void Configure()
    {
        Post("/login");
        AllowAnonymous();
        Group<AuthGroup>();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        {
            ThrowError("Username and password are required.", 400);
        }

        var user = await _userManager.FindByNameAsync(req.Username.Trim());
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

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
