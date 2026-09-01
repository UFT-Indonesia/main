using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.UseCases.Leave.Common;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.UseCases;

public class LeaveQuotaTests
{
    private static Employee NewEmployee(
        EmployeeRole role = EmployeeRole.Staff,
        LocalDate? hireDate = null)
    {
        var parentId = role == EmployeeRole.Owner ? (Erp.SharedKernel.Identity.EmployeeId?)null
            : Erp.SharedKernel.Identity.EmployeeId.New();

        return Employee.Create(
            "Budi",
            Nik.Create("3201234567890123"),
            Money.Idr(5_000_000m),
            new LocalDate(2026, 1, 1),
            role,
            parentId,
            hireDate: hireDate);
    }

    // ---- the formula ----------------------------------------------------

    [Fact]
    public void No_probation_date_means_a_full_year()
    {
        LeaveQuota.AnnualEntitlement(null, 2026).Should().Be(12);
    }

    [Fact]
    public void Confirmed_in_an_earlier_year_means_a_full_year()
    {
        LeaveQuota.AnnualEntitlement(new LocalDate(2025, 6, 1), 2026).Should().Be(12);
    }

    [Fact]
    public void Still_on_probation_all_year_means_nothing()
    {
        LeaveQuota.AnnualEntitlement(new LocalDate(2027, 3, 1), 2026).Should().Be(0);
    }

    [Theory]
    [InlineData(6, 1, 6)]    // confirmed 1 June   → Jul–Dec
    [InlineData(6, 30, 6)]   // the day of the month does not matter
    [InlineData(1, 15, 11)]  // confirmed in January → 11 months left
    [InlineData(12, 15, 0)]  // confirmed in December → nothing left this year
    public void Graduation_month_itself_is_dropped(int month, int day, int expected)
    {
        LeaveQuota.AnnualEntitlement(new LocalDate(2026, month, day), 2026).Should().Be(expected);
    }

    // ---- Entitled: probation, overrides, owner --------------------------

    [Fact]
    public void Probation_zeroes_annual_but_leaves_other_types_alone()
    {
        var employee = NewEmployee(hireDate: new LocalDate(2026, 5, 1));
        var duringProbation = new LocalDate(2026, 6, 1);

        // Only Annual is withheld during probation. The others are flat company-wide caps —
        // a probationer with flu gets the same thirty sick days as anyone else.
        LeaveQuota.Entitled(LeaveType.Annual, employee, 2026, duringProbation).Should().Be(0);
        LeaveQuota.Entitled(LeaveType.Sick, employee, 2026, duringProbation).Should().Be(30);
        LeaveQuota.Entitled(LeaveType.Permission, employee, 2026, duringProbation).Should().Be(6);
        LeaveQuota.Entitled(LeaveType.Unpaid, employee, 2026, duringProbation).Should().Be(30);
    }

    [Fact]
    public void Probation_ends_on_its_end_date_not_after_it()
    {
        var employee = NewEmployee(hireDate: new LocalDate(2026, 5, 1));

        employee.ProbationEndsOn.Should().Be(new LocalDate(2026, 8, 1));
        employee.IsOnProbation(new LocalDate(2026, 7, 31)).Should().BeTrue();
        employee.IsOnProbation(new LocalDate(2026, 8, 1)).Should().BeFalse();
    }

    [Fact]
    public void A_null_hire_date_is_never_on_probation()
    {
        var employee = NewEmployee();

        employee.ProbationEndsOn.Should().BeNull();
        employee.IsOnProbation(new LocalDate(2026, 6, 1)).Should().BeFalse();
        LeaveQuota.Entitled(LeaveType.Annual, employee, 2026, new LocalDate(2026, 6, 1)).Should().Be(12);
    }

    [Fact]
    public void Probation_beats_an_override()
    {
        var employee = NewEmployee(hireDate: new LocalDate(2026, 5, 1));
        employee.SetLeaveQuota(LeaveType.Annual, 20);

        LeaveQuota.Entitled(LeaveType.Annual, employee, 2026, new LocalDate(2026, 6, 1)).Should().Be(0);
    }

    [Fact]
    public void An_override_skips_proration_once_confirmed()
    {
        var employee = NewEmployee(hireDate: new LocalDate(2026, 5, 1));
        employee.SetLeaveQuota(LeaveType.Annual, 20);

        // Formula alone would give 12 - 8 = 4 for a 1 August confirmation.
        LeaveQuota.Entitled(LeaveType.Annual, employee, 2026, new LocalDate(2026, 9, 1)).Should().Be(20);
    }

    [Fact]
    public void Zero_is_a_real_override_and_clearing_restores_the_default()
    {
        var employee = NewEmployee();
        var today = new LocalDate(2026, 6, 1);

        employee.SetLeaveQuota(LeaveType.Sick, 0);
        LeaveQuota.Entitled(LeaveType.Sick, employee, 2026, today).Should().Be(0);

        // Clearing falls back to the company default, not to "uncapped".
        employee.SetLeaveQuota(LeaveType.Sick, null);
        LeaveQuota.Entitled(LeaveType.Sick, employee, 2026, today).Should().Be(LeaveQuota.FullSickDays);
    }

    [Fact]
    public void Every_type_has_a_company_default_and_an_override_replaces_it()
    {
        var employee = NewEmployee();
        var today = new LocalDate(2026, 6, 1);

        LeaveQuota.Entitled(LeaveType.Sick, employee, 2026, today).Should().Be(30);
        LeaveQuota.Entitled(LeaveType.Permission, employee, 2026, today).Should().Be(6);
        LeaveQuota.Entitled(LeaveType.Unpaid, employee, 2026, today).Should().Be(30);

        employee.SetLeaveQuota(LeaveType.Permission, 15);
        LeaveQuota.Entitled(LeaveType.Permission, employee, 2026, today).Should().Be(15);
    }

    [Fact]
    public void An_owner_is_exempt_from_probation_and_from_every_cap()
    {
        var owner = NewEmployee(EmployeeRole.Owner, hireDate: new LocalDate(2026, 5, 1));
        owner.SetLeaveQuota(LeaveType.Annual, 5);

        LeaveQuota.Entitled(LeaveType.Annual, owner, 2026, new LocalDate(2026, 6, 1)).Should().BeNull();
        LeaveQuota.Entitled(LeaveType.Sick, owner, 2026, new LocalDate(2026, 6, 1)).Should().BeNull();
    }

    [Fact]
    public void An_owner_override_of_the_probation_end_wins_over_the_hire_date_default()
    {
        var employee = NewEmployee(hireDate: new LocalDate(2026, 5, 1));
        employee.OverrideProbationEnd(new LocalDate(2026, 11, 1));

        employee.ProbationEndsOn.Should().Be(new LocalDate(2026, 11, 1));
        LeaveQuota.Entitled(LeaveType.Annual, employee, 2026, new LocalDate(2026, 12, 1)).Should().Be(1);

        // Correcting the hire date afterwards must not move a date someone set deliberately.
        employee.SetHireDate(new LocalDate(2026, 1, 1));
        employee.ProbationEndsOn.Should().Be(new LocalDate(2026, 11, 1));
    }

    // ---- per-year attribution -------------------------------------------

    [Fact]
    public void A_request_over_new_year_charges_each_year_its_own_days()
    {
        // Mon 28 Dec 2026 – Fri 8 Jan 2027: 4 workdays in 2026, 6 in 2027.
        var request = LeaveRequest.Create(
            Erp.SharedKernel.Identity.EmployeeId.New(),
            LeaveType.Annual,
            new LocalDate(2026, 12, 28),
            new LocalDate(2027, 1, 8),
            "cuti",
            null,
            Guid.NewGuid(),
            Instant.FromUtc(2026, 12, 1, 0, 0));

        request.WorkdayCount.Should().Be(10);
        LeaveQuota.WorkdaysInYear(request, 2026).Should().Be(4);
        LeaveQuota.WorkdaysInYear(request, 2027).Should().Be(6);
    }

    [Fact]
    public void Used_days_count_only_the_matching_type()
    {
        var employeeId = Erp.SharedKernel.Identity.EmployeeId.New();
        var annual = LeaveRequest.Create(
            employeeId, LeaveType.Annual,
            new LocalDate(2026, 3, 2), new LocalDate(2026, 3, 6),
            "cuti", null, Guid.NewGuid(), Instant.FromUtc(2026, 1, 1, 0, 0));
        var sick = LeaveRequest.Create(
            employeeId, LeaveType.Sick,
            new LocalDate(2026, 4, 6), new LocalDate(2026, 4, 7),
            "sakit", TestAttachments.DoctorsNote(), Guid.NewGuid(),
            Instant.FromUtc(2026, 1, 1, 0, 0));

        LeaveRequest[] approved = [annual, sick];

        LeaveQuota.UsedDays(approved, LeaveType.Annual, 2026).Should().Be(5);
        LeaveQuota.UsedDays(approved, LeaveType.Sick, 2026).Should().Be(2);
        LeaveQuota.UsedDaysAllTypes(approved, 2026).Should().Be(7);
        LeaveQuota.UsedDaysAllTypes(approved, 2025).Should().Be(0);
    }
}
