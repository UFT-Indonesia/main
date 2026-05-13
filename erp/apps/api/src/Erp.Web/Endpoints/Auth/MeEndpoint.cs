using System.Security.Claims;
using Erp.Infrastructure.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

namespace Erp.Web.Endpoints.Auth;

public sealed class MeEndpoint : EndpointWithoutRequest<AuthUserResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public MeEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Get("/me");
        Group<AuthGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var roles = await _userManager.GetRolesAsync(user);

        await SendOkAsync(new AuthUserResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            EmployeeId = user.EmployeeId,
            Roles = roles,
        }, ct);
    }
}
