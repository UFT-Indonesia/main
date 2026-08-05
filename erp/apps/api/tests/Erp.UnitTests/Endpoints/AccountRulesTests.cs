using System.Security.Claims;
using Erp.Core.Aggregates.Employees;
using Erp.Web.Endpoints.Accounts;
using FluentAssertions;

namespace Erp.UnitTests.Endpoints;

/// <summary>
/// The whole caller-vs-target matrix, because this one method is the permission
/// boundary for both the Accounts and the Employees endpoints.
/// </summary>
public class AccountRulesTests
{
    private static ClaimsPrincipal Caller(params string[] roles) =>
        new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "test"));

    [Theory]
    [InlineData(EmployeeRole.Owner)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Staff)]
    public void Owner_can_manage_any_role(EmployeeRole target)
    {
        AccountRules.CanManage(Caller(nameof(EmployeeRole.Owner)), target).Should().BeTrue();
    }

    [Theory]
    [InlineData(EmployeeRole.Owner, false)]
    [InlineData(EmployeeRole.Manager, false)]
    [InlineData(EmployeeRole.Staff, true)]
    public void Manager_can_manage_staff_only(EmployeeRole target, bool expected)
    {
        AccountRules.CanManage(Caller(nameof(EmployeeRole.Manager)), target).Should().Be(expected);
    }

    [Theory]
    [InlineData(EmployeeRole.Owner)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Staff)]
    public void Staff_can_manage_nobody(EmployeeRole target)
    {
        AccountRules.CanManage(Caller(nameof(EmployeeRole.Staff)), target).Should().BeFalse();
    }

    [Theory]
    [InlineData(EmployeeRole.Owner)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Staff)]
    public void Caller_without_roles_can_manage_nobody(EmployeeRole target)
    {
        AccountRules.CanManage(Caller(), target).Should().BeFalse();
    }

    [Fact]
    public void Owner_role_wins_when_caller_holds_several()
    {
        var caller = Caller(nameof(EmployeeRole.Manager), nameof(EmployeeRole.Owner));

        AccountRules.CanManage(caller, EmployeeRole.Owner).Should().BeTrue();
    }
}
