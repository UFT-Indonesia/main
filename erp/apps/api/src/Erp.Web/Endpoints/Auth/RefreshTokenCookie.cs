using Erp.Infrastructure.Authentication;
using NodaTime;

namespace Erp.Web.Endpoints.Auth;

internal static class RefreshTokenCookie
{
    public const string Name = "erp_refresh";

    public static void Append(
        HttpContext httpContext,
        RefreshTokenIssueResult refreshToken,
        IHostEnvironment environment)
    {
        Append(httpContext, refreshToken.PlainTextToken, refreshToken.ExpiresAtUtc, environment);
    }

    public static void Append(
        HttpContext httpContext,
        string plainTextToken,
        Instant expiresAtUtc,
        IHostEnvironment environment)
    {
        httpContext.Response.Cookies.Append(Name, plainTextToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = expiresAtUtc.ToDateTimeOffset(),
        });
    }

    public static void Clear(HttpContext httpContext, IHostEnvironment environment)
    {
        httpContext.Response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
        });
    }
}
