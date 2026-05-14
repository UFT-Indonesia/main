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

    public LoginEndpoint(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
    }

    public override void Configure()
    {
        Post("/login");
        AllowAnonymous();
        Group<AuthGroup>();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        {
            ThrowError("Email and password are required.", 400);
        }

        var user = await _userManager.FindByEmailAsync(req.Email.Trim());
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

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.CreateAccessToken(user, roles);

        await SendOkAsync(new AuthResponse
        {
            AccessToken = token.AccessToken,
            TokenType = "Bearer",
            ExpiresAtUtc = token.ExpiresAtUtc,
            User = new AuthUserResponse
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                EmployeeId = user.EmployeeId,
                Roles = roles,
            },
        }, ct);
    }
}
