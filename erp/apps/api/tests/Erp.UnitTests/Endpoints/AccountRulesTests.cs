using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.Web.Endpoints.Accounts;
using FluentAssertions;

namespace Erp.UnitTests.Endpoints;

/// <summary>
/// The whole caller-vs-target matrix, because these two methods are the permission boundary
/// for both the Accounts and the Employees endpoints. They answer different questions:
/// CanManage is scoped to the reporting line, CanGrantRole only to privilege.
/// </summary>
public class AccountRulesTests
{
    private static readonly EmployeeId ManagerId = EmployeeId.New();
    private static readonly EmployeeId OtherManagerId = EmployeeId.New();

    private static Caller Caller(EmployeeRole role, EmployeeId? employeeId = null) =>
        new(Guid.NewGuid(), role, employeeId, "Test Caller");

    [Theory]
    [InlineData(EmployeeRole.Owner)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Staff)]
    public void Owner_can_manage_any_role_anywhere_in_the_chart(EmployeeRole target)
    {
        AccountRules.CanManage(Caller(EmployeeRole.Owner), target, OtherManagerId.Value)
            .Should().BeTrue();
    }

    [Fact]
    public void Manager_can_manage_their_own_direct_staff()
    {
        AccountRules.CanManage(
            Caller(EmployeeRole.Manager, ManagerId), EmployeeRole.Staff, ManagerId.Value)
            .Should().BeTrue();
    }

    [Fact]
    public void Manager_cannot_manage_staff_in_another_line()
    {
        AccountRules.CanManage(
            Caller(EmployeeRole.Manager, ManagerId), EmployeeRole.Staff, OtherManagerId.Value)
            .Should().BeFalse("staff outside their line are the owner's to handle");
    }

    [Fact]
    public void Manager_cannot_manage_staff_with_no_manager_assigned()
    {
        AccountRules.CanManage(
            Caller(EmployeeRole.Manager, ManagerId), EmployeeRole.Staff, null)
            .Should().BeFalse("an unassigned staff member waits for the owner to place them");
    }

    [Theory]
    [InlineData(EmployeeRole.Owner)]
    [InlineData(EmployeeRole.Manager)]
    public void Manager_cannot_manage_peers_or_the_owner(EmployeeRole target)
    {
        AccountRules.CanManage(Caller(EmployeeRole.Manager, ManagerId), target, ManagerId.Value)
            .Should().BeFalse();
    }

    [Fact]
    public void Manager_without_an_employee_record_can_manage_nobody()
    {
        AccountRules.CanManage(Caller(EmployeeRole.Manager), EmployeeRole.Staff, ManagerId.Value)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(EmployeeRole.Owner)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Staff)]
    public void Staff_can_manage_nobody(EmployeeRole target)
    {
        AccountRules.CanManage(Caller(EmployeeRole.Staff, EmployeeId.New()), target, ManagerId.Value)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(EmployeeRole.Owner, true)]
    [InlineData(EmployeeRole.Manager, true)]
    [InlineData(EmployeeRole.Staff, true)]
    public void Owner_can_grant_any_role(EmployeeRole role, bool expected)
    {
        AccountRules.CanGrantRole(Caller(EmployeeRole.Owner), role).Should().Be(expected);
    }

    [Theory]
    [InlineData(EmployeeRole.Owner, false)]
    [InlineData(EmployeeRole.Manager, false)]
    [InlineData(EmployeeRole.Staff, true)]
    public void Manager_can_only_grant_staff(EmployeeRole role, bool expected)
    {
        // Line-agnostic on purpose: this guards privilege escalation, not who reports to whom.
        AccountRules.CanGrantRole(Caller(EmployeeRole.Manager, ManagerId), role)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(EmployeeRole.Owner)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Staff)]
    public void Staff_can_grant_nothing(EmployeeRole role)
    {
        AccountRules.CanGrantRole(Caller(EmployeeRole.Staff, EmployeeId.New()), role)
            .Should().BeFalse();
    }
}
