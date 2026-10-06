using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Overtime;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using NodaTime;
using NodaTime.Text;

namespace Erp.UnitTests.Domain;

/// <summary>One test row per row of GSS08's examples table, then the punch-ownership table.</summary>
public class OvertimeCalculatorTests
{
    private static readonly DateTimeZone Zone = DateTimeZoneProviders.Tzdb["Asia/Jakarta"];
    private static readonly EmployeeId Employee = EmployeeId.New();
    private static readonly LocalDate Tuesday = new(2026, 10, 6);
    private static readonly LocalDate Saturday = new(2026, 10, 10);
    private static readonly LocalDate Sunday = new(2026, 10, 11);

    private static readonly OvertimeTier[] Tiers = [new(2, 25_000), new(4, 50_000), new(6, 70_000)];

    private static AttendanceDayPolicy Policy(int grace) =>
        TestPolicies.Standard with { ClockInGraceMinutes = grace, ClockOutGraceMinutes = grace };

    private static OvertimeAssignment Assignment(LocalDate date, string? start, string end, bool dayOff) =>
        OvertimeAssignment.Create(
            Employee, date, start is null ? null : Time(start), Time(end), dayOff, Guid.NewGuid(), "Owner", Instant.FromUtc(2026, 10, 1, 0, 0), autoApprove: true);

    private static LocalTime Time(string hhmm) => LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(hhmm).Value;

    /// <summary>"0 18:32" is 18:32 on the work date, "1 00:57" is 00:57 the next day.</summary>
    private static Instant At(LocalDate date, string spec)
    {
        var parts = spec.Split(' ');
        return date.PlusDays(int.Parse(parts[0])).At(Time(parts[1])).InZoneLeniently(Zone).ToInstant();
    }

    [Theory]
    // weekday, grace 5
    [InlineData(false, "18:30", "21:30", 5, "0 18:32", "0 21:35", 3, 25_000, false, false)]
    [InlineData(false, "18:30", "21:30", 5, "0 18:36", "0 21:30", 2, 25_000, true, false)]
    [InlineData(false, "18:30", "01:00", 5, "0 18:31", "1 00:57", 6, 70_000, false, false)]
    [InlineData(false, "18:30", "01:00", 5, "0 18:31", "0 22:10", 3, 25_000, false, true)]
    [InlineData(false, "18:30", "01:00", 5, "0 18:31", "0 20:15", 1, 0, false, true)]
    [InlineData(false, "18:30", "01:00", 5, "0 18:31", "1 01:40", 6, 70_000, false, false)]
    [InlineData(false, "18:30", "21:30", 5, "0 18:36", "0 20:35", 1, 0, true, true)]
    [InlineData(false, "18:30", "21:30", 5, "0 18:20", "0 21:30", 3, 25_000, false, false)] // early tap-in counts from 18:30
    // day off
    [InlineData(true, "09:00", "16:00", 5, "0 09:10", "0 16:00", 5, 50_000, true, false)]
    [InlineData(true, "09:00", "16:00", 10, "0 09:10", "0 16:00", 6, 70_000, false, false)]
    [InlineData(true, "12:30", "14:45", 5, "0 12:30", "0 14:45", 2, 25_000, false, false)]
    [InlineData(true, "12:00", "16:00", 5, "0 12:04", "0 16:00", 3, 25_000, false, false)]
    public void Counts_hours_and_pays_the_tier(
        bool dayOff, string start, string end, int grace, string tapIn, string tapOut,
        int hours, int pay, bool late, bool leftEarly)
    {
        var date = dayOff ? Saturday : Tuesday;
        var assignment = Assignment(date, dayOff ? start : null, end, dayOff);

        var count = OvertimeCalculator.Count(assignment, At(date, tapIn), At(date, tapOut), Policy(grace));

        count.Should().Be(new OvertimeCount(hours, late, leftEarly, false));
        OvertimeCalculator.Pay(count.Hours, Tiers).Should().Be(pay);
    }

    [Fact]
    public void Sunday_lunch_is_only_deducted_when_fully_inside_the_counted_span()
    {
        var partial = Assignment(Sunday, "12:30", "14:45", dayOff: true);
        OvertimeCalculator.Count(partial, At(Sunday, "0 12:30"), At(Sunday, "0 14:45"), Policy(5)).Hours.Should().Be(2);

        var full = Assignment(Sunday, "11:00", "14:00", dayOff: true);
        OvertimeCalculator.Count(full, At(Sunday, "0 11:00"), At(Sunday, "0 14:00"), Policy(5)).Hours.Should().Be(2);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void A_missing_tap_is_incomplete_and_pays_nothing(bool missingIn, bool missingOut)
    {
        var assignment = Assignment(Tuesday, null, "21:30", dayOff: false);

        var count = OvertimeCalculator.Count(
            assignment, missingIn ? null : At(Tuesday, "0 18:31"), missingOut ? null : At(Tuesday, "0 21:30"), Policy(5));

        count.Should().Be(new OvertimeCount(0, false, false, true));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 25_000)]
    [InlineData(3, 25_000)]
    [InlineData(4, 50_000)]
    [InlineData(5, 50_000)]
    [InlineData(6, 70_000)]
    [InlineData(9, 70_000)]
    public void Pay_is_the_highest_tier_the_day_reaches(int hours, int expected) =>
        OvertimeCalculator.Pay(hours, Tiers).Should().Be(expected);

    private static AttendanceLog Punch(LocalDate date, string spec, PunchType type) =>
        AttendanceLog.Manual(Employee, At(date, spec), type, Guid.NewGuid());

    private static string[] Owned(OvertimeAssignment a, params AttendanceLog[] punches) =>
        OvertimeCalculator.OwnedPunches(a, punches, Policy(5))
            .Select(p => p.PunchedAtUtc.InZone(Zone).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

    [Fact]
    public void Weekday_overtime_claims_from_the_first_In_after_the_shift()
    {
        var a = Assignment(Tuesday, null, "01:00", dayOff: false);

        Owned(a,
                Punch(Tuesday, "0 07:55", PunchType.In), Punch(Tuesday, "0 18:00", PunchType.Out),
                Punch(Tuesday, "0 18:31", PunchType.In), Punch(Tuesday, "0 21:35", PunchType.Out))
            .Should().Equal("18:31", "21:35");
    }

    [Fact]
    public void A_regular_Out_just_after_the_shift_stays_with_the_regular_day()
    {
        var a = Assignment(Tuesday, null, "21:30", dayOff: false);

        Owned(a, Punch(Tuesday, "0 08:00", PunchType.In), Punch(Tuesday, "0 18:02", PunchType.Out)).Should().BeEmpty();
    }

    [Fact]
    public void Curious_repeated_taps_are_all_claimed_and_the_last_is_the_tap_out()
    {
        var a = Assignment(Tuesday, null, "01:00", dayOff: false);
        var punches = new[]
        {
            Punch(Tuesday, "0 18:31", PunchType.In), Punch(Tuesday, "1 00:58", PunchType.Out),
            Punch(Tuesday, "1 00:59", PunchType.In), Punch(Tuesday, "1 01:00", PunchType.Out),
            Punch(Tuesday, "1 01:01", PunchType.In),
        };

        var owned = OvertimeCalculator.OwnedPunches(a, punches, Policy(5));

        owned.Should().HaveCount(5);
        OvertimeCalculator.Taps(owned).TapOut.Should().Be(At(Tuesday, "1 01:01"));
    }

    [Fact]
    public void A_forgotten_tap_out_is_not_filled_by_the_next_morning()
    {
        var a = Assignment(Tuesday, null, "01:00", dayOff: false);
        var owned = OvertimeCalculator.OwnedPunches(
            a, [Punch(Tuesday, "0 18:31", PunchType.In), Punch(Tuesday, "1 06:40", PunchType.In)], Policy(5));

        owned.Should().ContainSingle();
        OvertimeCalculator.Count(a, OvertimeCalculator.Taps(owned).TapIn, OvertimeCalculator.Taps(owned).TapOut, Policy(5))
            .Incomplete.Should().BeTrue();
    }

    [Fact]
    public void Day_off_overtime_claims_everything_from_0500_to_0500()
    {
        var a = Assignment(Saturday, "09:00", "16:00", dayOff: true);

        Owned(a,
                Punch(Saturday, "0 04:00", PunchType.In), Punch(Saturday, "0 08:55", PunchType.In),
                Punch(Saturday, "0 16:02", PunchType.Out), Punch(Saturday, "1 04:59", PunchType.Out),
                Punch(Saturday, "1 05:00", PunchType.In))
            .Should().Equal("08:55", "16:02", "04:59");
    }

    [Fact]
    public void A_single_owned_punch_is_a_tap_in_without_a_tap_out()
    {
        var a = Assignment(Saturday, "09:00", "16:00", dayOff: true);

        OvertimeCalculator.Taps(OvertimeCalculator.OwnedPunches(a, [Punch(Saturday, "0 09:00", PunchType.In)], Policy(5)))
            .Should().Be((At(Saturday, "0 09:00"), (Instant?)null));
    }
}
