using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.Domain;

public class LeaveDeductionCalculatorTests
{
    private static readonly Guid Trip = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid March = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Later = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static Instant At(int month, int day) => Instant.FromUtc(2026, month, day, 3, 0);

    private static LeaveDeductionRequest Req(
        Guid id, LocalDate start, LocalDate end, Instant at, LeaveType type = LeaveType.Annual, decimal charge = 1m) =>
        new(id, type, start, end, charge, at);

    private static decimal? Cap12(LeaveType type, int year) => type switch
    {
        LeaveType.Annual => 12m,
        LeaveType.Sick => 30m,
        LeaveType.Permission => 6m,
        _ => 30m,
    };

    private static IReadOnlyList<LeaveDeductionDay> Allocate(
        IEnumerable<LeaveDeductionRequest> requests,
        Func<LeaveType, int, decimal?>? cap = null,
        IReadOnlyDictionary<(Guid, LocalDate), (decimal, decimal)>? frozen = null) =>
        LeaveDeductionCalculator.Allocate(requests, cap ?? Cap12, TestPolicies.Standard, frozen);

    private static readonly LeaveDeductionRequest TripRequest =
        Req(Trip, new LocalDate(2026, 11, 2), new LocalDate(2026, 11, 13), At(1, 15));

    private static readonly LeaveDeductionRequest MarchRequest =
        Req(March, new LocalDate(2026, 3, 9), new LocalDate(2026, 3, 12), At(3, 1));

    // ---- worked example (GSS03) ----------------------------------------

    [Fact]
    public void Trip_of_ten_days_is_entirely_free()
    {
        var days = Allocate([TripRequest]);

        days.Should().HaveCount(10);
        days.Sum(d => d.FreeDays).Should().Be(10);
        days.Sum(d => d.CutDays).Should().Be(0);
    }

    [Fact]
    public void Request_that_pushes_over_the_cap_pays_with_its_last_days()
    {
        var days = Allocate([TripRequest, MarchRequest]).Where(d => d.RequestId == March).ToList();

        days.Select(d => (d.Date.Day, d.FreeDays, d.CutDays)).Should().Equal(
            (9, 1m, 0m), (10, 1m, 0m), (11, 0m, 1m), (12, 0m, 1m));
    }

    [Fact]
    public void Approval_order_decides_not_leave_date()
    {
        // Same two requests, but March is approved first: March is free, the November trip is cut.
        var marchFirst = MarchRequest with { EffectiveAt = At(1, 2) };

        var days = Allocate([TripRequest, marchFirst]);

        days.Where(d => d.RequestId == March).Sum(d => d.CutDays).Should().Be(0);
        days.Where(d => d.RequestId == Trip).Sum(d => d.CutDays).Should().Be(2);
    }

    [Fact]
    public void Closed_days_keep_their_label_after_the_earlier_trip_is_cancelled()
    {
        var frozen = new Dictionary<(Guid, LocalDate), (decimal, decimal)>
        {
            [(March, new LocalDate(2026, 3, 9))] = (1m, 0m),
            [(March, new LocalDate(2026, 3, 10))] = (1m, 0m),
            [(March, new LocalDate(2026, 3, 11))] = (0m, 1m),
            [(March, new LocalDate(2026, 3, 12))] = (0m, 1m),
        };

        // The trip is gone; March alone would be all free, but March is closed.
        var days = Allocate([MarchRequest], frozen: frozen);

        days.Sum(d => d.CutDays).Should().Be(2);
        days.Sum(d => d.FreeDays).Should().Be(2);
    }

    [Fact]
    public void Cut_days_do_not_use_up_the_cap()
    {
        var frozen = new Dictionary<(Guid, LocalDate), (decimal, decimal)>
        {
            [(March, new LocalDate(2026, 3, 9))] = (1m, 0m),
            [(March, new LocalDate(2026, 3, 10))] = (1m, 0m),
            [(March, new LocalDate(2026, 3, 11))] = (0m, 1m),
            [(March, new LocalDate(2026, 3, 12))] = (0m, 1m),
        };
        var later = Req(Later, new LocalDate(2026, 11, 2), new LocalDate(2026, 11, 13), At(6, 1));

        // Trip cancelled: 2 free days were spent in March, so 10 of the later request's 10 fit.
        var days = Allocate([MarchRequest, later], frozen: frozen);

        days.Where(d => d.RequestId == Later).Sum(d => d.CutDays).Should().Be(0);
    }

    // ---- shapes ---------------------------------------------------------

    [Fact]
    public void A_request_across_months_cuts_in_the_month_each_day_falls_in()
    {
        var spent = Req(Trip, new LocalDate(2026, 1, 5), new LocalDate(2026, 1, 16), At(1, 1)); // 10 days
        var cross = Req(March, new LocalDate(2026, 3, 30), new LocalDate(2026, 4, 2), At(3, 1)); // Mon–Thu

        var days = Allocate([spent, cross]).Where(d => d.RequestId == March).ToList();

        days.Where(d => d.Date.Month == 3).Sum(d => d.CutDays).Should().Be(0); // 30–31 Mar still free
        days.Where(d => d.Date.Month == 4).Sum(d => d.CutDays).Should().Be(2); // 1–2 Apr cut
    }

    [Fact]
    public void New_Year_charges_each_year_its_own_cap()
    {
        var yearEnd = Req(Trip, new LocalDate(2026, 12, 28), new LocalDate(2027, 1, 8), At(11, 1)); // 10 workdays

        var days = Allocate([yearEnd], cap: (_, year) => year == 2026 ? 2m : 12m);

        days.Where(d => d.Date.Year == 2026).Sum(d => d.CutDays).Should().Be(2); // 28–31 Dec is 4 days; 2 free
        days.Where(d => d.Date.Year == 2027).Sum(d => d.CutDays).Should().Be(0);
    }

    [Fact]
    public void Half_day_costs_half_a_cut_day_when_the_cap_is_gone()
    {
        var ten = Req(Trip, new LocalDate(2026, 1, 5), new LocalDate(2026, 1, 16), At(1, 1));
        var twoMore = Req(Later, new LocalDate(2026, 2, 2), new LocalDate(2026, 2, 3), At(2, 1));
        var half = Req(March, new LocalDate(2026, 3, 2), new LocalDate(2026, 3, 2), At(3, 1), charge: 0.5m);

        var days = Allocate([ten, twoMore, half]).Where(d => d.RequestId == March).ToList();

        days.Single().CutDays.Should().Be(0.5m);
    }

    [Fact]
    public void A_fraction_can_straddle_the_cap()
    {
        var cap = 1.25m;
        var fractions = Req(Trip, new LocalDate(2026, 3, 2), new LocalDate(2026, 3, 3), At(3, 1), charge: 0.75m);

        var days = Allocate([fractions], cap: (_, _) => cap);

        days.Select(d => (d.FreeDays, d.CutDays)).Should().Equal((0.75m, 0m), (0.5m, 0.25m));
    }

    [Fact]
    public void Hourly_izin_counts_its_fraction()
    {
        var izin = Req(Trip, new LocalDate(2026, 3, 2), new LocalDate(2026, 3, 2), At(3, 1), LeaveType.Permission, 0.25m);

        var days = Allocate([izin], cap: (_, _) => 0m);

        days.Single().CutDays.Should().Be(0.25m);
    }

    [Fact]
    public void Editing_requeues_the_request_at_the_edit_time()
    {
        // The trip was approved first, but extended in June: it now queues after March and pays for itself.
        var editedTrip = TripRequest with { EffectiveAt = At(6, 1) };

        var days = Allocate([editedTrip, MarchRequest]);

        days.Where(d => d.RequestId == March).Sum(d => d.CutDays).Should().Be(0);
        days.Where(d => d.RequestId == Trip).Sum(d => d.CutDays).Should().Be(2);
    }

    [Fact]
    public void Every_unpaid_day_is_cut_and_leaves_the_cap_alone()
    {
        var unpaid = Req(Later, new LocalDate(2026, 2, 2), new LocalDate(2026, 2, 4), At(2, 1), LeaveType.Unpaid);

        var days = Allocate([unpaid, TripRequest]);

        days.Where(d => d.RequestId == Later).Sum(d => d.CutDays).Should().Be(3);
        days.Where(d => d.RequestId == Trip).Sum(d => d.CutDays).Should().Be(0);
    }

    [Fact]
    public void Sick_and_izin_are_cut_past_their_own_caps()
    {
        var sick = Req(Trip, new LocalDate(2026, 3, 2), new LocalDate(2026, 3, 4), At(3, 1), LeaveType.Sick);

        var days = Allocate([sick], cap: (type, _) => type == LeaveType.Sick ? 1m : 12m);

        days.Sum(d => d.CutDays).Should().Be(2);
    }

    [Fact]
    public void Uncapped_leave_is_never_cut()
    {
        var days = Allocate([TripRequest, MarchRequest], cap: (_, _) => null);

        days.Sum(d => d.CutDays).Should().Be(0);
    }

    // ---- money ----------------------------------------------------------

    [Fact]
    public void Daily_rate_is_salary_over_the_divisor()
    {
        LeaveDeductionCalculator.DailyRate(5_000_000m, 20, null).Should().Be(250_000m);
    }

    [Fact]
    public void Flat_exception_overrides_the_salary_and_zero_exempts()
    {
        var flat = LeaveDeductionException.Create(100_000m, null);
        var exempt = LeaveDeductionException.Create(0m, null);

        LeaveDeductionCalculator.DailyRate(5_000_000m, 20, flat).Should().Be(100_000m);
        LeaveDeductionCalculator.DailyRate(5_000_000m, 20, exempt).Should().Be(0m);
    }

    [Fact]
    public void Custom_divisor_replaces_the_company_one()
    {
        var custom = LeaveDeductionException.Create(null, 25);

        LeaveDeductionCalculator.DailyRate(5_000_000m, 20, custom).Should().Be(200_000m);
    }

    [Fact]
    public void Daily_rate_is_kept_to_the_decimals_a_frozen_line_stores()
    {
        // 3.000.000 / 21 = 142.857,142857…; stored at 4 decimals it is 142.857,1429, and 7 days of it
        // cross Rp 1.000.000. The open-month preview must use the same figure, or it floors to 999.000.
        var rate = LeaveDeductionCalculator.DailyRate(3_000_000m, 21, null);

        rate.Should().Be(142_857.1429m);
        LeaveDeductionCalculator.MonthTotal(Enumerable.Repeat(rate, 7)).Should().Be(1_000_000m);
    }

    [Fact]
    public void Divisors_above_a_months_length_are_refused()
    {
        var settings = new PayrollSettings(new LocalDate(2026, 1, 1));

        settings.SetDivisor(PayrollSettings.MaxDivisor);
        settings.Divisor.Should().Be(31);
        FluentActions.Invoking(() => settings.SetDivisor(32)).Should().Throw<Erp.SharedKernel.Domain.Errors.DomainException>();
        FluentActions.Invoking(() => settings.SetDivisor(0)).Should().Throw<Erp.SharedKernel.Domain.Errors.DomainException>();
        FluentActions.Invoking(() => LeaveDeductionException.Create(null, 32)).Should().Throw<Erp.SharedKernel.Domain.Errors.DomainException>();
    }

    [Fact]
    public void Month_total_rounds_down_once()
    {
        // Salary 4.555.555 / 20 = 227.777,75; one full and one half day = 341.666,625 → 341.000.
        var rate = LeaveDeductionCalculator.DailyRate(4_555_555m, 20, null);

        LeaveDeductionCalculator.MonthTotal([1m * rate, 0.5m * rate]).Should().Be(341_000m);
    }

    [Fact]
    public void Salary_in_effect_on_the_day_is_used()
    {
        var history = new[]
        {
            new SalaryPoint(new LocalDate(2026, 1, 1), 5_000_000m),
            new SalaryPoint(new LocalDate(2026, 4, 1), 6_000_000m),
        };

        // A raise entered on 25 Mar effective 1 Apr never touches March.
        LeaveDeductionCalculator.SalaryOn(history, new LocalDate(2026, 3, 31), 6_000_000m).Should().Be(5_000_000m);
        LeaveDeductionCalculator.SalaryOn(history, new LocalDate(2026, 4, 1), 6_000_000m).Should().Be(6_000_000m);
    }

    [Fact]
    public void Salary_before_the_first_record_uses_the_first_record()
    {
        var history = new[] { new SalaryPoint(new LocalDate(2026, 6, 1), 5_000_000m) };

        LeaveDeductionCalculator.SalaryOn(history, new LocalDate(2026, 3, 1), 9m).Should().Be(5_000_000m);
    }

    [Fact]
    public void No_history_falls_back_to_the_current_salary()
    {
        LeaveDeductionCalculator.SalaryOn([], new LocalDate(2026, 3, 1), 4_000_000m).Should().Be(4_000_000m);
    }

    [Fact]
    public void An_exception_cannot_be_both_flat_and_divisor()
    {
        var act = () => LeaveDeductionException.Create(1m, 20);

        act.Should().Throw<Erp.SharedKernel.Domain.Errors.DomainException>();
    }

    [Fact]
    public void Empty_exception_means_none()
    {
        LeaveDeductionException.Create(null, null).Should().BeNull();
    }
}
