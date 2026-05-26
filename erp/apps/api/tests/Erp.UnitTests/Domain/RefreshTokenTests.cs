using Erp.Core.Aggregates.Auth;
using Erp.SharedKernel.Domain.Errors;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.Domain;

public class RefreshTokenTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 26, 12, 0);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Issue_creates_refresh_token_with_valid_parameters()
    {
        var token = RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            "192.168.1.1",
            "Mozilla/5.0");

        token.UserId.Should().Be(UserId);
        token.TokenHash.Should().Be("hash123");
        token.CreatedAtUtc.Should().Be(Now);
        token.ExpiresAtUtc.Should().Be(Now.Plus(Duration.FromDays(14)));
        token.CreatedByIp.Should().Be("192.168.1.1");
        token.CreatedByUserAgent.Should().Be("Mozilla/5.0");
        token.IsRevoked.Should().BeFalse();
        token.FamilyId.Should().NotBeEmpty();
    }

    [Fact]
    public void Issue_throws_for_empty_user_id()
    {
        var act = () => RefreshToken.Issue(
            Guid.Empty,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            null);

        act.Should().Throw<DomainException>()
            .WithMessage("*User id is required*");
    }

    [Fact]
    public void Issue_throws_for_empty_token_hash()
    {
        var act = () => RefreshToken.Issue(
            UserId,
            "",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            null);

        act.Should().Throw<DomainException>()
            .WithMessage("*hash*");
    }

    [Fact]
    public void Issue_throws_for_expiry_before_creation()
    {
        var act = () => RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Minus(Duration.FromDays(1)),
            null,
            null);

        act.Should().Throw<DomainException>()
            .WithMessage("*expiry*");
    }

    [Fact]
    public void Issue_throws_for_ip_address_too_long()
    {
        var longIp = new string('x', 65);

        var act = () => RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            longIp,
            null);

        act.Should().Throw<DomainException>()
            .WithMessage("*IP address exceeds maximum length*");
    }

    [Fact]
    public void Issue_throws_for_user_agent_too_long()
    {
        var longUserAgent = new string('x', 513);

        var act = () => RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            longUserAgent);

        act.Should().Throw<DomainException>()
            .WithMessage("*User agent exceeds maximum length*");
    }

    [Fact]
    public void Issue_accepts_max_length_ip_and_user_agent()
    {
        var maxIp = new string('x', 64);
        var maxUserAgent = new string('y', 512);

        var token = RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            maxIp,
            maxUserAgent);

        token.CreatedByIp.Should().HaveLength(64);
        token.CreatedByUserAgent.Should().HaveLength(512);
    }

    [Fact]
    public void Issue_uses_provided_family_id()
    {
        var familyId = Guid.NewGuid();

        var token = RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            null,
            familyId);

        token.FamilyId.Should().Be(familyId);
    }

    [Fact]
    public void IsExpired_returns_true_when_past_expiry()
    {
        var token = RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            null);

        token.IsExpired(Now.Plus(Duration.FromDays(15))).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_returns_false_when_before_expiry()
    {
        var token = RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            null);

        token.IsExpired(Now.Plus(Duration.FromDays(13))).Should().BeFalse();
    }

    [Fact]
    public void Revoke_marks_token_as_revoked()
    {
        var token = RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            null);

        token.Revoke(Now.Plus(Duration.FromHours(1)), "user_logout");

        token.IsRevoked.Should().BeTrue();
        token.RevokedReason.Should().Be("user_logout");
        token.RevokedAtUtc.Should().Be(Now.Plus(Duration.FromHours(1)));
    }

    [Fact]
    public void Revoke_is_idempotent()
    {
        var token = RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            null);

        token.Revoke(Now.Plus(Duration.FromHours(1)), "first");
        token.Revoke(Now.Plus(Duration.FromHours(2)), "second");

        token.RevokedReason.Should().Be("first");
        token.RevokedAtUtc.Should().Be(Now.Plus(Duration.FromHours(1)));
    }

    [Fact]
    public void Revoke_throws_for_empty_reason()
    {
        var token = RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            null);

        var act = () => token.Revoke(Now, "");

        act.Should().Throw<DomainException>()
            .WithMessage("*Revocation reason is required*");
    }

    [Fact]
    public void IsActive_returns_true_for_valid_non_revoked_token()
    {
        var token = RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            null);

        token.IsActive(Now.Plus(Duration.FromDays(1))).Should().BeTrue();
    }

    [Fact]
    public void IsActive_returns_false_for_revoked_token()
    {
        var token = RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            null);

        token.Revoke(Now, "test");

        token.IsActive(Now.Plus(Duration.FromDays(1))).Should().BeFalse();
    }

    [Fact]
    public void IsActive_returns_false_for_expired_token()
    {
        var token = RefreshToken.Issue(
            UserId,
            "hash123",
            Now,
            Now.Plus(Duration.FromDays(14)),
            null,
            null);

        token.IsActive(Now.Plus(Duration.FromDays(15))).Should().BeFalse();
    }
}
