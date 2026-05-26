using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Auth;

public sealed class RefreshToken : Entity<RefreshTokenId>
{
    private RefreshToken() { }

    private RefreshToken(
        RefreshTokenId id,
        Guid userId,
        string tokenHash,
        Guid familyId,
        Instant createdAtUtc,
        Instant expiresAtUtc,
        string? createdByIp,
        string? createdByUserAgent)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
        CreatedByUserAgent = createdByUserAgent;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid FamilyId { get; private set; }

    public Instant CreatedAtUtc { get; private set; }

    public Instant ExpiresAtUtc { get; private set; }

    public string? CreatedByIp { get; private set; }

    public string? CreatedByUserAgent { get; private set; }

    public Instant? RevokedAtUtc { get; private set; }

    public string? RevokedReason { get; private set; }

    public RefreshTokenId? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsExpired(Instant nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsActive(Instant nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        Instant createdAtUtc,
        Instant expiresAtUtc,
        string? createdByIp,
        string? createdByUserAgent,
        Guid? familyId = null,
        RefreshTokenId? id = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("refresh_token.user_id", "User id is required.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("refresh_token.hash", "Token hash is required.");
        }

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new DomainException("refresh_token.expiry", "Refresh token expiry must be after creation time.");
        }

        var normalizedIp = Normalize(createdByIp);
        var normalizedUserAgent = Normalize(createdByUserAgent);

        if (normalizedIp?.Length > 64)
        {
            throw new DomainException("refresh_token.ip_too_long", "IP address exceeds maximum length of 64 characters.");
        }

        if (normalizedUserAgent?.Length > 512)
        {
            throw new DomainException("refresh_token.user_agent_too_long", "User agent exceeds maximum length of 512 characters.");
        }

        return new RefreshToken(
            id ?? RefreshTokenId.New(),
            userId,
            tokenHash.Trim(),
            familyId ?? Guid.NewGuid(),
            createdAtUtc,
            expiresAtUtc,
            normalizedIp,
            normalizedUserAgent);
    }

    public void Revoke(Instant revokedAtUtc, string reason)
    {
        if (IsRevoked)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("refresh_token.revoke_reason", "Revocation reason is required.");
        }

        RevokedAtUtc = revokedAtUtc;
        RevokedReason = reason.Trim();
    }

    public void MarkReplacedBy(RefreshTokenId replacementTokenId, Instant revokedAtUtc)
    {
        if (IsRevoked)
        {
            throw new DomainException("refresh_token.revoked", "Refresh token is already revoked.");
        }

        if (replacementTokenId == RefreshTokenId.Empty)
        {
            throw new DomainException("refresh_token.replacement", "Replacement token id is required.");
        }

        if (replacementTokenId == Id)
        {
            throw new DomainException("refresh_token.replacement_self", "Refresh token cannot replace itself.");
        }

        ReplacedByTokenId = replacementTokenId;
        Revoke(revokedAtUtc, "rotated");
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
