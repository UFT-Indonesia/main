using Erp.Core.Aggregates.Auth;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.Infrastructure;

public class RefreshTokenServiceTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClock _clock;
    private readonly JwtOptions _jwtOptions;
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 26, 12, 0);

    public RefreshTokenServiceTests()
    {
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            null, null, null, null, null, null, null, null);

        _clock = Substitute.For<IClock>();
        _clock.GetCurrentInstant().Returns(Now);

        _jwtOptions = new JwtOptions { RefreshTokenDays = 14 };
    }

    [Fact]
    public async Task IssueAsync_creates_refresh_token_and_returns_plain_text()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com", FullName = "Test User" };

        var result = await _service.IssueAsync(user, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);

        result.PlainTextToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAtUtc.Should().Be(Now.Plus(Duration.FromDays(14)));

        var token = await _db.RefreshTokens.SingleAsync();
        token.UserId.Should().Be(user.Id);
        token.CreatedByIp.Should().Be("192.168.1.1");
        token.CreatedByUserAgent.Should().Be("Mozilla/5.0");
        token.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task IssueAsync_truncates_long_user_agent()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com", FullName = "Test User" };
        var longUserAgent = new string('x', 600);

        var result = await _service.IssueAsync(user, null, longUserAgent, CancellationToken.None);

        var token = await _db.RefreshTokens.SingleAsync();
        token.CreatedByUserAgent.Should().HaveLength(512);
    }

    [Fact]
    public async Task RotateAsync_returns_invalid_for_missing_token()
    {
        var result = await _service.RotateAsync("nonexistent-token", null, null, CancellationToken.None);

        result.Should().BeOfType<RefreshTokenRotationResult.Invalid>()
            .Which.Message.Should().Be("Refresh token is invalid.");
    }

    [Fact]
    public async Task RotateAsync_returns_invalid_for_expired_token()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com", FullName = "Test User" };
        var issueResult = await _service.IssueAsync(user, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);

        _clock.GetCurrentInstant().Returns(Now.Plus(Duration.FromDays(15)));

        var result = await _service.RotateAsync(issueResult.PlainTextToken, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);

        result.Should().BeOfType<RefreshTokenRotationResult.Invalid>()
            .Which.Message.Should().Be("Refresh token is expired.");

        var token = await _db.RefreshTokens.SingleAsync();
        token.IsRevoked.Should().BeTrue();
        token.RevokedReason.Should().Be("expired");
    }

    [Fact]
    public async Task RotateAsync_revokes_family_on_reused_token()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com", FullName = "Test User" };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.IsLockedOutAsync(user).Returns(false);

        var issueResult = await _service.IssueAsync(user, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);
        var firstRotate = await _service.RotateAsync(issueResult.PlainTextToken, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);
        firstRotate.Should().BeOfType<RefreshTokenRotationResult.Success>();

        var secondRotate = await _service.RotateAsync(issueResult.PlainTextToken, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);

        secondRotate.Should().BeOfType<RefreshTokenRotationResult.Compromised>()
            .Which.Message.Should().Contain("reused");

        var tokens = await _db.RefreshTokens.ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Should().OnlyContain(t => t.IsRevoked);
    }

    [Fact]
    public async Task RotateAsync_detects_concurrent_rotation_and_revokes_family()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com", FullName = "Test User" };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.IsLockedOutAsync(user).Returns(false);

        var issueResult = await _service.IssueAsync(user, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);

        var firstRotate = _service.RotateAsync(issueResult.PlainTextToken, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);
        var secondRotate = _service.RotateAsync(issueResult.PlainTextToken, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);

        var results = await Task.WhenAll(firstRotate, secondRotate);

        results.Should().ContainSingle(r => r is RefreshTokenRotationResult.Success);
        results.Should().ContainSingle(r => r is RefreshTokenRotationResult.Compromised);

        var tokens = await _db.RefreshTokens.ToListAsync();
        tokens.Should().OnlyContain(t => t.IsRevoked);
    }

    [Fact]
    public async Task RotateAsync_succeeds_and_creates_replacement_in_same_family()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com", FullName = "Test User" };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.IsLockedOutAsync(user).Returns(false);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Owner" });

        var issueResult = await _service.IssueAsync(user, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);
        var originalToken = await _db.RefreshTokens.SingleAsync();

        var result = await _service.RotateAsync(issueResult.PlainTextToken, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);

        result.Should().BeOfType<RefreshTokenRotationResult.Success>();
        var success = (RefreshTokenRotationResult.Success)result;
        success.User.Should().Be(user);
        success.PlainTextToken.Should().NotBe(issueResult.PlainTextToken);

        var tokens = await _db.RefreshTokens.ToListAsync();
        tokens.Should().HaveCount(2);

        var oldToken = tokens.Single(t => t.Id == originalToken.Id);
        oldToken.IsRevoked.Should().BeTrue();
        oldToken.RevokedReason.Should().Be("rotated");

        var newToken = tokens.Single(t => t.Id != originalToken.Id);
        newToken.IsRevoked.Should().BeFalse();
        newToken.FamilyId.Should().Be(originalToken.FamilyId);
        newToken.ExpiresAtUtc.Should().Be(originalToken.ExpiresAtUtc);
    }

    [Fact]
    public async Task RotateAsync_revokes_family_for_suspicious_access()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com", FullName = "Test User" };
        var issueResult = await _service.IssueAsync(user, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);

        _clock.GetCurrentInstant().Returns(Now.Plus(Duration.FromMinutes(2)));

        var result = await _service.RotateAsync(issueResult.PlainTextToken, "10.0.0.1", "Chrome/100", CancellationToken.None);

        result.Should().BeOfType<RefreshTokenRotationResult.Compromised>()
            .Which.Message.Should().Contain("suspicious");

        var token = await _db.RefreshTokens.SingleAsync();
        token.IsRevoked.Should().BeTrue();
        token.RevokedReason.Should().Be("compromised");
    }

    [Fact]
    public async Task RotateAsync_returns_invalid_for_locked_user()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com", FullName = "Test User" };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.IsLockedOutAsync(user).Returns(true);

        var issueResult = await _service.IssueAsync(user, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);

        var result = await _service.RotateAsync(issueResult.PlainTextToken, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);

        result.Should().BeOfType<RefreshTokenRotationResult.Invalid>()
            .Which.Message.Should().Contain("locked");

        var tokens = await _db.RefreshTokens.ToListAsync();
        tokens.Should().OnlyContain(t => t.IsRevoked);
    }

    [Fact]
    public async Task RevokeAllForUserAsync_revokes_all_active_tokens()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com", FullName = "Test User" };

        await _service.IssueAsync(user, "192.168.1.1", "Mozilla/5.0", CancellationToken.None);
        await _service.IssueAsync(user, "192.168.1.2", "Chrome/100", CancellationToken.None);

        await _service.RevokeAllForUserAsync(user.Id, "password_changed", CancellationToken.None);

        var tokens = await _db.RefreshTokens.ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Should().OnlyContain(t => t.IsRevoked && t.RevokedReason == "password_changed");
    }
}
