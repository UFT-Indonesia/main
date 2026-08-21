using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.Infrastructure;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _service;

    public JwtTokenServiceTests()
    {
        var clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 8, 5, 9, 0));

        var options = new JwtOptions
        {
            Issuer = "erp.test",
            Audience = "erp.test",
            SigningKey = Convert.ToBase64String(new byte[32]),
            AccessTokenMinutes = 60,
            RefreshTokenDays = 14,
        };

        _service = new JwtTokenService(Options.Create(options), clock);
    }

    private static JwtSecurityToken Decode(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

    [Fact]
    public void Stamps_the_employee_role_as_the_only_role_claim()
    {
        var user = new ApplicationUser { FullName = "Budi" };

        var token = _service.CreateAccessToken(
            user, new AccountIdentity(EmployeeRole.Manager, Guid.NewGuid(), null));

        var roles = Decode(token.AccessToken).Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value);
        roles.Should().ContainSingle().Which.Should().Be(nameof(EmployeeRole.Manager));
    }

    [Fact]
    public void Stamps_the_employee_id_claim_when_the_account_is_linked()
    {
        var employeeId = Guid.NewGuid();

        var token = _service.CreateAccessToken(
            new ApplicationUser { FullName = "Budi" },
            new AccountIdentity(EmployeeRole.Staff, employeeId, null));

        Decode(token.AccessToken).Claims
            .Single(claim => claim.Type == JwtTokenService.EmployeeIdClaim)
            .Value.Should().Be(employeeId.ToString());
    }

    [Fact]
    public void Omits_the_employee_id_claim_when_the_account_is_unlinked()
    {
        var token = _service.CreateAccessToken(
            new ApplicationUser { FullName = "System" },
            new AccountIdentity(EmployeeRole.Owner, null, null));

        Decode(token.AccessToken).Claims
            .Should().NotContain(claim => claim.Type == JwtTokenService.EmployeeIdClaim);
    }

    [Fact]
    public void Flags_accounts_still_holding_a_temporary_password()
    {
        var token = _service.CreateAccessToken(
            new ApplicationUser { FullName = "Budi", MustChangePassword = true },
            new AccountIdentity(EmployeeRole.Staff, Guid.NewGuid(), null));

        Decode(token.AccessToken).Claims
            .Single(claim => claim.Type == JwtTokenService.MustChangePasswordClaim)
            .Value.Should().Be("true");
    }
}
