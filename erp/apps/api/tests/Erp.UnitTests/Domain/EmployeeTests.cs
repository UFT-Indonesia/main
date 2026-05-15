using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.Domain;

public class EmployeeTests
{
    private static readonly Nik SampleNik = Nik.Create("3201234567890123");
    private static readonly Money Wage = Money.Idr(8_000_000m);
    private static readonly LocalDate EffectiveFrom = new(2025, 1, 1);

    [Fact]
    public void Create_owner_succeeds_without_parent()
    {
        var owner = Employee.Create(
            "Owner Satu",
            SampleNik,
            Wage,
            EffectiveFrom,
            EmployeeRole.Owner);

        owner.Role.Should().Be(EmployeeRole.Owner);
        owner.ParentId.Should().BeNull();
        owner.Status.Should().Be(EmployeeStatus.Active);
        owner.DomainEvents.Should().ContainSingle(e => e is EmployeeCreated);
    }

    [Fact]
    public void Create_owner_with_parent_throws()
    {
        var parentId = EmployeeId.New();

        var act = () => Employee.Create(
            "Owner Salah",
            SampleNik,
            Wage,
            EffectiveFrom,
            EmployeeRole.Owner,
            parentId: parentId);

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.owner_no_parent");
    }

    [Fact]
    public void Create_non_owner_without_parent_throws()
    {
        var act = () => Employee.Create(
            "Manager Tanpa Parent",
            SampleNik,
            Wage,
            EffectiveFrom,
            EmployeeRole.Manager);

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.parent_required");
    }

    [Fact]
    public void Create_rejects_zero_wage()
    {
        var act = () => Employee.Create(
            "Pekerja Nol",
            SampleNik,
            Money.Idr(0m),
            EffectiveFrom,
            EmployeeRole.Owner);

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.wage");
    }

    [Fact]
    public void Create_rejects_blank_name()
    {
        var act = () => Employee.Create(
            "  ",
            SampleNik,
            Wage,
            EffectiveFrom,
            EmployeeRole.Owner);

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.full_name");
    }

    [Fact]
    public void UpdateBasicInfo_records_event_when_full_name_changes()
    {
        var employee = NewOwner();

        employee.UpdateBasicInfo("Owner Baru", null);

        employee.FullName.Should().Be("Owner Baru");
        employee.DomainEvents.OfType<EmployeeBasicInfoChanged>().Should().HaveCount(1);
    }

    [Fact]
    public void UpdateBasicInfo_assigns_npwp_when_provided()
    {
        var employee = NewOwner();
        var npwp = Npwp.Create("123456789012345");

        employee.UpdateBasicInfo(employee.FullName, npwp);

        employee.Npwp.Should().Be(npwp);
        employee.DomainEvents.OfType<EmployeeBasicInfoChanged>().Should().HaveCount(1);
    }

    [Fact]
    public void UpdateBasicInfo_idempotent_when_unchanged()
    {
        var employee = NewOwner();

        employee.UpdateBasicInfo(employee.FullName, employee.Npwp);

        employee.DomainEvents.OfType<EmployeeBasicInfoChanged>().Should().BeEmpty();
    }

    [Fact]
    public void UpdateBasicInfo_rejects_blank_name()
    {
        var employee = NewOwner();

        var act = () => employee.UpdateBasicInfo("  ", null);

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.full_name");
    }

    [Fact]
    public void UpdateBasicInfo_rejects_terminated_employee()
    {
        var manager = NewManager(out _);
        manager.Terminate(EffectiveFrom.PlusDays(10));

        var act = () => manager.UpdateBasicInfo("Nama Baru", null);

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.terminated");
    }

    [Fact]
    public void ChangeSalary_records_event_and_updates_state()
    {
        var employee = NewOwner();
        var newWage = Money.Idr(10_000_000m);
        var newEffective = new LocalDate(2025, 6, 1);

        employee.ChangeSalary(newWage, newEffective);

        employee.MonthlyWage.Should().Be(newWage);
        employee.EffectiveSalaryFrom.Should().Be(newEffective);
        employee.DomainEvents.OfType<EmployeeSalaryChanged>().Should().HaveCount(1);
    }

    [Fact]
    public void ChangeSalary_rejects_backdated_effective()
    {
        var employee = NewOwner();

        var act = () => employee.ChangeSalary(Money.Idr(9_000_000m), new LocalDate(2024, 12, 31));

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.salary_backdated");
    }

    [Fact]
    public void AssignParent_rejects_self()
    {
        var manager = NewManager(out _);
        var act = () => manager.AssignParent(manager.Id);

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.parent_self");
    }

    [Fact]
    public void AssignParent_rejects_owner_with_parent()
    {
        var owner = NewOwner();
        var act = () => owner.AssignParent(EmployeeId.New());

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.owner_no_parent");
    }

    [Fact]
    public void AssignParent_idempotent_when_unchanged()
    {
        var manager = NewManager(out var ownerId);

        manager.AssignParent(ownerId);

        manager.DomainEvents.OfType<EmployeeParentChanged>().Should().BeEmpty();
    }

    [Fact]
    public void AssignParent_records_event_when_changed()
    {
        var manager = NewManager(out _);
        var newParentId = EmployeeId.New();

        manager.AssignParent(newParentId);

        manager.DomainEvents.OfType<EmployeeParentChanged>().Should().HaveCount(1);
        manager.ParentId.Should().Be(newParentId);
    }

    [Fact]
    public void ChangeRole_to_owner_requires_no_parent()
    {
        var manager = NewManager(out _);

        var act = () => manager.ChangeRole(EmployeeRole.Owner);

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.owner_no_parent");
    }

    [Fact]
    public void Terminate_blocks_subsequent_mutations()
    {
        var manager = NewManager(out _);
        manager.Terminate(EffectiveFrom.PlusDays(30));

        var act = () => manager.ChangeSalary(Money.Idr(20_000_000m), EffectiveFrom.PlusMonths(2));

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.terminated");
    }

    [Fact]
    public void Terminate_twice_throws()
    {
        var manager = NewManager(out _);
        manager.Terminate(EffectiveFrom.PlusDays(30));

        var act = () => manager.Terminate(EffectiveFrom.PlusDays(60));

        act.Should().Throw<DomainException>().Where(e => e.Code == "employee.already_terminated");
    }

    private static Employee NewOwner() =>
        Employee.Create("Owner", SampleNik, Wage, EffectiveFrom, EmployeeRole.Owner);

    private static Employee NewManager(out EmployeeId ownerId)
    {
        ownerId = EmployeeId.New();
        return Employee.Create(
            "Manager",
            Nik.Create("3201234567890124"),
            Wage,
            EffectiveFrom,
            EmployeeRole.Manager,
            parentId: ownerId);
    }
}
