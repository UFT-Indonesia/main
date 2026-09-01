using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.UseCases;

/// <summary>The full authority matrix, since these four predicates are the whole leave permission model.</summary>
public class LeaveRulesTests
{
    private static readonly EmployeeId OwnerId = EmployeeId.New();
    private static readonly EmployeeId ManagerId = EmployeeId.New();
    private static readonly EmployeeId OtherManagerId = EmployeeId.New();

    private static Employee Subject(EmployeeRole role, EmployeeId? parentId) => Employee.Create(
        $"{role} Subject",
        Nik.Create("3201234567890123"),
        Money.Idr(5_000_000m),
        new LocalDate(2026, 1, 1),
        role,
        parentId);

    private static Caller CallerFor(EmployeeRole role, EmployeeId? employeeId) =>
        new(Guid.NewGuid(), role, employeeId, "Caller");

    [Fact]
    public void Owners_own_leave_is_auto_approved_regardless_of_who_files_it()
    {
        LeaveRules.IsAutoApproved(EmployeeRole.Owner, EmployeeRole.Owner).Should().BeTrue();
        LeaveRules.IsAutoApproved(EmployeeRole.Owner, EmployeeRole.Manager).Should().BeTrue();
        LeaveRules.IsAutoApproved(EmployeeRole.Owner, EmployeeRole.Staff).Should().BeTrue();
    }

    [Fact]
    public void Leave_an_owner_files_for_someone_else_is_also_auto_approved()
    {
        LeaveRules.IsAutoApproved(EmployeeRole.Manager, EmployeeRole.Owner).Should().BeTrue();
        LeaveRules.IsAutoApproved(EmployeeRole.Staff, EmployeeRole.Owner).Should().BeTrue();
    }

    [Fact]
    public void Leave_filed_by_a_non_owner_for_a_non_owner_is_not_auto_approved()
    {
        LeaveRules.IsAutoApproved(EmployeeRole.Manager, EmployeeRole.Manager).Should().BeFalse();
        LeaveRules.IsAutoApproved(EmployeeRole.Staff, EmployeeRole.Manager).Should().BeFalse();
        LeaveRules.IsAutoApproved(EmployeeRole.Staff, EmployeeRole.Staff).Should().BeFalse();
    }

    [Fact]
    public void Owner_may_file_for_anyone()
    {
        var owner = CallerFor(EmployeeRole.Owner, OwnerId);

        LeaveRules.CanFileFor(owner, Subject(EmployeeRole.Staff, ManagerId)).Should().BeTrue();
        LeaveRules.CanFileFor(owner, Subject(EmployeeRole.Manager, OwnerId)).Should().BeTrue();
        LeaveRules.CanFileFor(owner, Subject(EmployeeRole.Owner, null)).Should().BeTrue();
    }

    [Fact]
    public void Manager_may_file_only_for_themselves_and_their_own_staff()
    {
        var manager = CallerFor(EmployeeRole.Manager, ManagerId);
        var ownStaff = Subject(EmployeeRole.Staff, ManagerId);
        var otherStaff = Subject(EmployeeRole.Staff, OtherManagerId);

        LeaveRules.CanFileFor(manager, ownStaff).Should().BeTrue();
        LeaveRules.CanFileFor(manager, otherStaff).Should().BeFalse();
        LeaveRules.CanFileFor(manager, Subject(EmployeeRole.Manager, OwnerId)).Should().BeFalse();
        LeaveRules.CanFileFor(manager, Subject(EmployeeRole.Owner, null)).Should().BeFalse();
    }

    [Fact]
    public void Staff_may_file_only_for_themselves()
    {
        var staffEmployee = Subject(EmployeeRole.Staff, ManagerId);
        var self = CallerFor(EmployeeRole.Staff, staffEmployee.Id);
        var someoneElse = CallerFor(EmployeeRole.Staff, EmployeeId.New());

        LeaveRules.CanFileFor(self, staffEmployee).Should().BeTrue();
        LeaveRules.CanFileFor(someoneElse, staffEmployee).Should().BeFalse();
    }

    [Fact]
    public void Staff_leave_is_decided_by_their_own_manager_or_any_owner()
    {
        var staffEmployee = Subject(EmployeeRole.Staff, ManagerId);

        LeaveRules.CanDecideFor(CallerFor(EmployeeRole.Owner, OwnerId), staffEmployee).Should().BeTrue();
        LeaveRules.CanDecideFor(CallerFor(EmployeeRole.Manager, ManagerId), staffEmployee).Should().BeTrue();
        LeaveRules.CanDecideFor(CallerFor(EmployeeRole.Manager, OtherManagerId), staffEmployee).Should().BeFalse();
        LeaveRules.CanDecideFor(CallerFor(EmployeeRole.Staff, staffEmployee.Id), staffEmployee).Should().BeFalse();
    }

    [Fact]
    public void Manager_leave_is_decided_only_by_an_owner()
    {
        var managerEmployee = Subject(EmployeeRole.Manager, OwnerId);

        LeaveRules.CanDecideFor(CallerFor(EmployeeRole.Owner, OwnerId), managerEmployee).Should().BeTrue();
        LeaveRules.CanDecideFor(CallerFor(EmployeeRole.Manager, managerEmployee.Id), managerEmployee).Should().BeFalse();
        LeaveRules.CanDecideFor(CallerFor(EmployeeRole.Manager, OtherManagerId), managerEmployee).Should().BeFalse();
    }

    [Fact]
    public void Owner_leave_is_never_decided_by_anyone()
    {
        var ownerEmployee = Subject(EmployeeRole.Owner, null);

        LeaveRules.CanDecideFor(CallerFor(EmployeeRole.Owner, ownerEmployee.Id), ownerEmployee).Should().BeFalse();
        LeaveRules.CanDecideFor(CallerFor(EmployeeRole.Owner, OwnerId), ownerEmployee).Should().BeFalse();
        LeaveRules.CanDecideFor(CallerFor(EmployeeRole.Manager, ManagerId), ownerEmployee).Should().BeFalse();
    }

    [Fact]
    public void An_owner_may_cancel_only_their_own_auto_approved_leave()
    {
        var ownerEmployee = Subject(EmployeeRole.Owner, null);

        LeaveRules.CanCancel(CallerFor(EmployeeRole.Owner, ownerEmployee.Id), ownerEmployee).Should().BeTrue();
        LeaveRules.CanCancel(CallerFor(EmployeeRole.Owner, OwnerId), ownerEmployee).Should().BeFalse();
    }

    [Fact]
    public void The_subject_may_always_cancel_their_own_leave()
    {
        var staffEmployee = Subject(EmployeeRole.Staff, ManagerId);
        var managerEmployee = Subject(EmployeeRole.Manager, OwnerId);

        LeaveRules.CanCancel(CallerFor(EmployeeRole.Staff, staffEmployee.Id), staffEmployee).Should().BeTrue();
        LeaveRules.CanCancel(CallerFor(EmployeeRole.Manager, managerEmployee.Id), managerEmployee).Should().BeTrue();
    }

    [Fact]
    public void Cancelling_someone_elses_leave_takes_approval_authority()
    {
        var staffEmployee = Subject(EmployeeRole.Staff, ManagerId);

        LeaveRules.CanCancel(CallerFor(EmployeeRole.Manager, ManagerId), staffEmployee).Should().BeTrue();
        LeaveRules.CanCancel(CallerFor(EmployeeRole.Manager, OtherManagerId), staffEmployee).Should().BeFalse();
        LeaveRules.CanCancel(CallerFor(EmployeeRole.Staff, EmployeeId.New()), staffEmployee).Should().BeFalse();
    }

    [Fact]
    public void An_account_with_no_employee_can_never_act_as_the_subject()
    {
        var staffEmployee = Subject(EmployeeRole.Staff, ManagerId);
        var unlinkedOwner = CallerFor(EmployeeRole.Owner, null);

        // Role authority still applies...
        LeaveRules.CanDecideFor(unlinkedOwner, staffEmployee).Should().BeTrue();
        // ...but it is nobody's "self", so it cannot file for a Manager-tier subject as self.
        LeaveRules.CanFileFor(CallerFor(EmployeeRole.Staff, null), staffEmployee).Should().BeFalse();
    }

    [Fact]
    public void The_requester_is_recognised_regardless_of_role()
    {
        var userId = Guid.NewGuid();
        var caller = new Caller(userId, EmployeeRole.Manager, ManagerId, "Manager");

        LeaveRules.IsRequester(caller, userId).Should().BeTrue();
        LeaveRules.IsRequester(caller, Guid.NewGuid()).Should().BeFalse();
    }
}
