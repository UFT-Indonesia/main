using Erp.Core.Aggregates.Overtime;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.Domain;

public class OvertimeAssignmentTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 1, 0, 0);
    private static readonly LocalDate Tuesday = new(2026, 10, 6);
    private static readonly LocalDate Saturday = new(2026, 10, 10);

    private static OvertimeAssignment Weekday(LocalTime end, bool autoApprove = true) =>
        OvertimeAssignment.Create(EmployeeId.New(), Tuesday, null, end, false, Guid.NewGuid(), "Owner", Now, autoApprove);

    private static OvertimeAssignment DayOff(LocalTime start, LocalTime end) =>
        OvertimeAssignment.Create(EmployeeId.New(), Saturday, start, end, true, Guid.NewGuid(), "Owner", Now, autoApprove: true);

    [Fact]
    public void Weekday_starts_at_1830_and_past_midnight_counts_on_the_start_date()
    {
        var a = Weekday(new LocalTime(1, 0));

        a.StartTime.Should().Be(new LocalTime(18, 30));
        a.Date.Should().Be(Tuesday);
        a.EndDate.Should().Be(Tuesday.PlusDays(1));
        a.Status.Should().Be(OvertimeStatus.Approved);
    }

    [Fact]
    public void Weekday_start_cannot_be_chosen() =>
        FluentActions.Invoking(() => OvertimeAssignment.Create(
                EmployeeId.New(), Tuesday, new LocalTime(19, 0), new LocalTime(22, 0), false, Guid.NewGuid(), "Owner", Now, true))
            .Should().Throw<DomainException>().Which.Code.Should().Be("overtime.start_fixed");

    [Theory]
    [InlineData(6, 0)] // after the 05:00 boundary but before the start
    [InlineData(18, 30)] // not after the start
    public void A_weekday_end_must_be_later_that_evening_or_by_0500(int hour, int minute) =>
        FluentActions.Invoking(() => Weekday(new LocalTime(hour, minute)))
            .Should().Throw<DomainException>();

    [Fact]
    public void A_day_off_cannot_start_before_the_0500_boundary() =>
        FluentActions.Invoking(() => DayOff(new LocalTime(4, 0), new LocalTime(9, 0)))
            .Should().Throw<DomainException>().Which.Code.Should().Be("overtime.start_before_boundary");

    [Fact]
    public void A_day_off_needs_a_start() =>
        FluentActions.Invoking(() => OvertimeAssignment.Create(
                EmployeeId.New(), Saturday, null, new LocalTime(16, 0), true, Guid.NewGuid(), "Owner", Now, true))
            .Should().Throw<DomainException>().Which.Code.Should().Be("overtime.start_required");

    [Fact]
    public void A_manager_made_assignment_waits_for_an_owner()
    {
        var a = Weekday(new LocalTime(21, 30), autoApprove: false);

        a.Status.Should().Be(OvertimeStatus.Pending);
        a.Approve(Guid.NewGuid(), "Owner", Now);
        a.Status.Should().Be(OvertimeStatus.Approved);
    }

    [Fact]
    public void Editing_the_end_as_a_manager_returns_it_to_pending()
    {
        var a = Weekday(new LocalTime(21, 30));

        a.ChangeEnd(new LocalTime(22, 30), hasPunches: false, returnToPending: true);

        a.Status.Should().Be(OvertimeStatus.Pending);
        a.EndTime.Should().Be(new LocalTime(22, 30));
    }

    [Fact]
    public void With_punches_the_end_can_grow_but_not_shrink()
    {
        var a = Weekday(new LocalTime(21, 30));

        FluentActions.Invoking(() => a.ChangeEnd(new LocalTime(20, 30), hasPunches: true, returnToPending: false))
            .Should().Throw<DomainException>().Which.Code.Should().Be("overtime.shorten_with_punches");

        a.ChangeEnd(new LocalTime(23, 0), hasPunches: true, returnToPending: false);
        a.EndTime.Should().Be(new LocalTime(23, 0));
    }

    [Fact]
    public void With_punches_it_can_no_longer_be_cancelled() =>
        FluentActions.Invoking(() => Weekday(new LocalTime(21, 30)).Cancel(Guid.NewGuid(), "Owner", Now, null, hasPunches: true))
            .Should().Throw<DomainException>().Which.Code.Should().Be("overtime.has_punches");

    [Fact]
    public void A_frozen_assignment_is_closed_to_every_change()
    {
        var a = Weekday(new LocalTime(21, 30));
        a.Freeze(3, 25_000, Now);

        FluentActions.Invoking(() => a.Cancel(Guid.NewGuid(), "Owner", Now, null, hasPunches: false))
            .Should().Throw<DomainException>().Which.Code.Should().Be("overtime.period_closed");
        FluentActions.Invoking(() => a.ChangeEnd(new LocalTime(22, 0), false, false))
            .Should().Throw<DomainException>().Which.Code.Should().Be("overtime.period_closed");
    }

    [Fact]
    public void An_undecided_assignment_expires_but_keeps_owning_its_punches()
    {
        var a = Weekday(new LocalTime(21, 30), autoApprove: false);

        a.Expire();

        a.Status.Should().Be(OvertimeStatus.Expired);
        a.OwnsPunches.Should().BeTrue();
        a.IsLive.Should().BeFalse();
    }

    [Theory]
    [InlineData(2026, 9, 14, 2026, 9, 1, 2026, 10, 31, 2026, 11, 15)]
    [InlineData(2026, 12, 31, 2026, 11, 1, 2026, 12, 31, 2027, 1, 15)]
    [InlineData(2027, 2, 28, 2027, 1, 1, 2027, 2, 28, 2027, 3, 15)]
    [InlineData(2028, 2, 1, 2028, 1, 1, 2028, 2, 29, 2028, 3, 15)]
    public void Periods_are_two_months_from_an_odd_month_paid_on_the_15th_after(
        int y, int m, int d, int sy, int sm, int sd, int ey, int em, int ed, int py, int pm, int pd)
    {
        var start = GajiPremiPeriod.StartOf(new LocalDate(y, m, d));

        start.Should().Be(new LocalDate(sy, sm, sd));
        GajiPremiPeriod.EndOf(start).Should().Be(new LocalDate(ey, em, ed));
        GajiPremiPeriod.PayoutDate(start).Should().Be(new LocalDate(py, pm, pd));
    }
}
