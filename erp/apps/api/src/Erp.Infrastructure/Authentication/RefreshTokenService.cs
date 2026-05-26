using System.Security.Cryptography;
using Erp.Core.Aggregates.Auth;
using Erp.Infrastructure.Identity;
using Erp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Erp.Infrastructure.Authentication;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private const int RefreshTokenByteLength = 64;
    private static readonly Duration SuspiciousAccessWindow = Duration.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClock _clock;
    private readonly JwtOptions _options;

    public RefreshTokenService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IClock clock,
        IOptions<JwtOptions> options)
    {
        _db = db;
        _userManager = userManager;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<RefreshTokenIssueResult> IssueAsync(
        ApplicationUser user,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = _clock.GetCurrentInstant();
        var expiresAt = now.Plus(Duration.FromDays(_options.RefreshTokenDays));
        var plainTextToken = GenerateOpaqueToken();
        var tokenHash = HashToken(plainTextToken);
        var refreshToken = RefreshToken.Issue(
            user.Id,
            tokenHash,
            now,
            expiresAt,
            ipAddress,
            userAgent);

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        return new RefreshTokenIssueResult(plainTextToken, expiresAt);
    }

    public async Task<RefreshTokenRotationResult> RotateAsync(
        string plainTextToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plainTextToken))
        {
            return new RefreshTokenRotationResult.Invalid("Refresh token is required.");
        }

        var tokenHash = HashToken(plainTextToken);
        var token = await _db.RefreshTokens
            .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, ct);

        if (token is null)
        {
            return new RefreshTokenRotationResult.Invalid("Refresh token is invalid.");
        }

        var now = _clock.GetCurrentInstant();
        if (token.IsRevoked)
        {
            await RevokeFamilyAsync(token.FamilyId, "compromised", ct);
            return new RefreshTokenRotationResult.Compromised(
                "Session revoked because the refresh token was reused. Please log in again.");
        }

        if (token.IsExpired(now))
        {
            token.Revoke(now, "expired");
            await _db.SaveChangesAsync(ct);
            return new RefreshTokenRotationResult.Invalid("Refresh token is expired.");
        }

        if (IsSuspiciousAccess(token, ipAddress, userAgent, now))
        {
            await RevokeFamilyAsync(token.FamilyId, "compromised", ct);
            return new RefreshTokenRotationResult.Compromised(
                "Session revoked due to suspicious activity. Please log in again.");
        }

        var user = await _userManager.FindByIdAsync(token.UserId.ToString());
        if (user is null)
        {
            await RevokeFamilyAsync(token.FamilyId, "user_missing", ct);
            return new RefreshTokenRotationResult.Invalid("User account is unavailable.");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            await RevokeFamilyAsync(token.FamilyId, "user_locked", ct);
            return new RefreshTokenRotationResult.Invalid("User account is locked.");
        }

        var replacementPlainTextToken = GenerateOpaqueToken();
        var replacement = RefreshToken.Issue(
            user.Id,
            HashToken(replacementPlainTextToken),
            now,
            token.ExpiresAtUtc,
            ipAddress,
            userAgent,
            token.FamilyId);

        token.MarkReplacedBy(replacement.Id, now);
        _db.RefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(ct);

        return new RefreshTokenRotationResult.Success(user, replacementPlainTextToken, replacement.ExpiresAtUtc);
    }

    public async Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Revocation reason is required.", nameof(reason));
        }

        var now = _clock.GetCurrentInstant();
        var tokens = await _db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.Revoke(now, reason);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeFamilyForTokenAsync(string plainTextToken, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plainTextToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Revocation reason is required.", nameof(reason));
        }

        var tokenHash = HashToken(plainTextToken);
        var token = await _db.RefreshTokens
            .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, ct);

        if (token is null)
        {
            return;
        }

        await RevokeFamilyAsync(token.FamilyId, reason, ct);
    }

    public async Task RevokeAllForEmployeeAsync(Guid employeeId, string reason, CancellationToken ct)
    {
        if (employeeId == Guid.Empty)
        {
            throw new ArgumentException("Employee id is required.", nameof(employeeId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Revocation reason is required.", nameof(reason));
        }

        var userIds = await _userManager.Users
            .Where(user => user.EmployeeId == employeeId)
            .Select(user => user.Id)
            .ToListAsync(ct);

        if (userIds.Count == 0)
        {
            return;
        }

        var now = _clock.GetCurrentInstant();
        var tokens = await _db.RefreshTokens
            .Where(token => userIds.Contains(token.UserId) && token.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.Revoke(now, reason);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct)
    {
        var now = _clock.GetCurrentInstant();
        var tokens = await _db.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.Revoke(now, reason);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static bool IsSuspiciousAccess(
        RefreshToken token,
        string? ipAddress,
        string? userAgent,
        Instant now)
    {
        if (now - token.CreatedAtUtc > SuspiciousAccessWindow)
        {
            return false;
        }

        return IsDifferent(token.CreatedByIp, ipAddress)
            && IsDifferent(token.CreatedByUserAgent, userAgent);
    }

    private static bool IsDifferent(string? known, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(known) || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return !string.Equals(known.Trim(), candidate.Trim(), StringComparison.Ordinal);
    }

    private static string GenerateOpaqueToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(RefreshTokenByteLength);
        return Base64UrlEncode(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
