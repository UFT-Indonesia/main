using System.Security.Claims;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Erp.Web.Endpoints.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapGet("/me", MeAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwtTokenService)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { code = "auth.invalid_request", message = "Email and password are required." });
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await CreateAuthResponseAsync(user, userManager, jwtTokenService));
    }

    private static async Task<IResult> MeAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(new AuthUserResponse(user.Id, user.Email ?? string.Empty, user.FullName, user.EmployeeId, roles));
    }

    private static async Task<AuthResponse> CreateAuthResponseAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService)
    {
        var roles = await userManager.GetRolesAsync(user);
        var token = jwtTokenService.CreateAccessToken(user, roles);
        return new AuthResponse(
            token.AccessToken,
            "Bearer",
            token.ExpiresAtUtc,
            new AuthUserResponse(user.Id, user.Email ?? string.Empty, user.FullName, user.EmployeeId, roles));
    }
}
