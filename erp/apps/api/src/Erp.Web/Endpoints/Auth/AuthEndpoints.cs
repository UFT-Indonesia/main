using System.Security.Claims;
using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Erp.Web.Endpoints.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/bootstrap-owner", BootstrapOwnerAsync).AllowAnonymous();
        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapGet("/me", MeAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> BootstrapOwnerAsync(
        BootstrapOwnerRequest request,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IJwtTokenService jwtTokenService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return Results.BadRequest(new { code = "auth.invalid_request", message = "Email, password, and full name are required." });
        }

        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            return Results.Conflict(new { code = "auth.bootstrap_locked", message = "Owner bootstrap is available only before the first user exists." });
        }

        await EnsureRoleAsync(roleManager, EmployeeRole.Owner.ToString());
        await EnsureRoleAsync(roleManager, EmployeeRole.Manager.ToString());
        await EnsureRoleAsync(roleManager, EmployeeRole.Staff.ToString());

        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Results.ValidationProblem(ToValidationErrors(createResult));
        }

        var roleResult = await userManager.AddToRoleAsync(user, EmployeeRole.Owner.ToString());
        if (!roleResult.Succeeded)
        {
            return Results.ValidationProblem(ToValidationErrors(roleResult));
        }

        return Results.Ok(await CreateAuthResponseAsync(user, userManager, jwtTokenService));
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

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole<Guid>> roleManager, string role)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create role '{role}'.");
            }
        }
    }

    private static Dictionary<string, string[]> ToValidationErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
}
