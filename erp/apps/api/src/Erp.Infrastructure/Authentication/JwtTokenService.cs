using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Erp.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NodaTime;

namespace Erp.Infrastructure.Authentication;

public sealed class JwtTokenService : IJwtTokenService
{
    /// <summary>Present (value "true") while the user must change a temporary password.</summary>
    public const string MustChangePasswordClaim = "pwd_change";

    /// <summary>
    /// The employee this account speaks for. Safe to cache in the token because the
    /// account→employee link is set once at provisioning and never rewritten; anything
    /// that does change (ParentId) is read fresh per request instead.
    /// </summary>
    public const string EmployeeIdClaim = "employee_id";

    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public JwtToken CreateAccessToken(ApplicationUser user, AccountIdentity identity)
    {
        var expiresAt = _clock.GetCurrentInstant()
            .Plus(Duration.FromMinutes(_options.AccessTokenMinutes))
            .ToDateTimeOffset();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
        };

        claims.Add(new Claim(ClaimTypes.Role, identity.Role.ToString()));

        if (identity.EmployeeId is { } employeeId)
        {
            claims.Add(new Claim(EmployeeIdClaim, employeeId.ToString()));
        }

        if (user.MustChangePassword)
        {
            claims.Add(new Claim(MustChangePasswordClaim, "true"));
        }

        var signingKey = new SymmetricSecurityKey(JwtSigningKeyHelper.DecodeSigningKey(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
