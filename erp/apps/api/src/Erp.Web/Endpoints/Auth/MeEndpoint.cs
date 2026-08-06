using System.Security.Claims;
using Erp.Infrastructure.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Erp.Web.Endpoints.Auth;

[Authorize]
public sealed class MeEndpoint : EndpointWithoutRequest<AuthUserResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountIdentityResolver _identityResolver;

    public MeEndpoint(UserManager<ApplicationUser> userManager, IAccountIdentityResolver identityResolver)
    {
        _userManager = userManager;
        _identityResolver = identityResolver;
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

        var identity = await _identityResolver.ResolveAsync(user, ct);

        await SendOkAsync(AuthUserResponse.From(user, identity), ct);
    }
}
