using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Common;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.UseCases;

/// <summary>
/// Attendance visibility is deliberately wider than attendance authority, so read and write
/// are asserted separately.
/// </summary>
public class AttendanceRulesTests
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
    public void Owner_and_manager_read_the_whole_company()
    {
        AttendanceRules.CanReadAll(CallerFor(EmployeeRole.Owner, OwnerId)).Should().BeTrue();
        AttendanceRules.CanReadAll(CallerFor(EmployeeRole.Manager, ManagerId)).Should().BeTrue();
        AttendanceRules.CanReadAll(CallerFor(EmployeeRole.Staff, EmployeeId.New())).Should().BeFalse();
    }

    [Fact]
    public void A_manager_may_read_someone_outside_their_own_line()
    {
        var stranger = EmployeeId.New();

        AttendanceRules.CanRead(CallerFor(EmployeeRole.Manager, ManagerId), stranger).Should().BeTrue();
    }

    [Fact]
    public void Staff_read_only_their_own_record()
    {
        var staffId = EmployeeId.New();
        var staff = CallerFor(EmployeeRole.Staff, staffId);

        AttendanceRules.CanRead(staff, staffId).Should().BeTrue();
        AttendanceRules.CanRead(staff, EmployeeId.New()).Should().BeFalse();
    }

    [Fact]
    public void An_unlinked_account_reads_nobody_unless_its_role_allows_all()
    {
        AttendanceRules.CanRead(CallerFor(EmployeeRole.Staff, null), EmployeeId.New()).Should().BeFalse();
        AttendanceRules.CanRead(CallerFor(EmployeeRole.Owner, null), EmployeeId.New()).Should().BeTrue();
    }

    [Fact]
    public void Owner_writes_for_anyone()
    {
        var owner = CallerFor(EmployeeRole.Owner, OwnerId);

        AttendanceRules.CanWriteFor(owner, Subject(EmployeeRole.Staff, OtherManagerId)).Should().BeTrue();
        AttendanceRules.CanWriteFor(owner, Subject(EmployeeRole.Manager, OwnerId)).Should().BeTrue();
        AttendanceRules.CanWriteFor(owner, Subject(EmployeeRole.Owner, null)).Should().BeTrue();
    }

    [Fact]
    public void Manager_writes_only_for_themselves_and_their_direct_staff()
    {
        var manager = CallerFor(EmployeeRole.Manager, ManagerId);
        var ownStaff = Subject(EmployeeRole.Staff, ManagerId);
        var foreignStaff = Subject(EmployeeRole.Staff, OtherManagerId);

        AttendanceRules.CanWriteFor(manager, ownStaff).Should().BeTrue();
        AttendanceRules.CanWriteFor(manager, foreignStaff).Should().BeFalse();
        AttendanceRules.CanWriteFor(manager, Subject(EmployeeRole.Manager, OwnerId)).Should().BeFalse();
        AttendanceRules.CanWriteFor(manager, Subject(EmployeeRole.Owner, null)).Should().BeFalse();
    }

    [Fact]
    public void A_manager_may_write_their_own_attendance()
    {
        var managerEmployee = Subject(EmployeeRole.Manager, OwnerId);
        var self = CallerFor(EmployeeRole.Manager, managerEmployee.Id);

        AttendanceRules.CanWriteFor(self, managerEmployee).Should().BeTrue();
    }

    [Fact]
    public void Staff_never_write_attendance_not_even_their_own()
    {
        var staffEmployee = Subject(EmployeeRole.Staff, ManagerId);
        var self = CallerFor(EmployeeRole.Staff, staffEmployee.Id);

        AttendanceRules.CanWriteFor(self, staffEmployee).Should().BeFalse();
    }
}
