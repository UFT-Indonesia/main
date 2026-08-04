using Erp.Infrastructure.Authentication;

namespace Erp.Web.Middleware.Authentication;

/// <summary>
/// While an access token carries the pwd_change claim, only /api/auth/*
/// (change-password, me, refresh, logout) is reachable; everything else is 403.
/// Keeps a temp password handed out by an admin from being usable against the API.
/// </summary>
public sealed class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true
            && user.HasClaim(JwtTokenService.MustChangePasswordClaim, "true")
            && !context.Request.Path.StartsWithSegments("/api/auth"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(new
            {
                code = "auth.password_change_required",
                message = "Password change required before accessing this resource.",
            });
        }

        return _next(context);
    }
}
