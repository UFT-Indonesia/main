using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.Domain;

public class AttendancePolicyTests
{
    private static AttendancePolicy CreatePolicy() => AttendancePolicy.Create(
        new LocalTime(9, 0),
        new LocalTime(18, 0),
        clockInGraceMinutes: 5,
        clockOutGraceMinutes: 5,
        "Asia/Jakarta",
        maxIzinHours: 4,
        Guid.Empty,
        Instant.FromUtc(2026, 1, 1, 0, 0));

    [Fact]
    public void Create_uses_the_fixed_singleton_id()
    {
        var policy = CreatePolicy();

        policy.Id.Should().Be(AttendancePolicyId.Singleton);
    }

    [Fact]
    public void Update_rejects_shift_start_not_before_shift_end()
    {
        var policy = CreatePolicy();

        var act = () => policy.Update(
            new LocalTime(18, 0),
            new LocalTime(9, 0),
            5,
            5,
            "Asia/Jakarta",
            4,
            Guid.NewGuid(),
            Instant.FromUtc(2026, 1, 2, 0, 0));

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance_policy.shift_window");
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, -1)]
    public void Update_rejects_negative_grace_minutes(int clockIn, int clockOut)
    {
        var policy = CreatePolicy();

        var act = () => policy.Update(
            new LocalTime(9, 0),
            new LocalTime(18, 0),
            clockIn,
            clockOut,
            "Asia/Jakarta",
            4,
            Guid.NewGuid(),
            Instant.FromUtc(2026, 1, 2, 0, 0));

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(9, 0, 10, 0)]  // exactly 60 minutes — leaves no room for the assumed lunch
    [InlineData(9, 0, 9, 30)]  // shorter still
    public void Update_rejects_a_shift_of_60_minutes_or_less(
        int startHour, int startMinute, int endHour, int endMinute)
    {
        var policy = CreatePolicy();

        var act = () => policy.Update(
            new LocalTime(startHour, startMinute),
            new LocalTime(endHour, endMinute),
            5,
            5,
            "Asia/Jakarta",
            4,
            Guid.NewGuid(),
            Instant.FromUtc(2026, 1, 2, 0, 0));

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance_policy.shift_too_short");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_rejects_non_positive_max_izin_hours(int maxIzinHours)
    {
        var policy = CreatePolicy();

        var act = () => policy.Update(
            new LocalTime(9, 0),
            new LocalTime(18, 0),
            5,
            5,
            "Asia/Jakarta",
            maxIzinHours,
            Guid.NewGuid(),
            Instant.FromUtc(2026, 1, 2, 0, 0));

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance_policy.max_izin_hours");
    }

    [Fact]
    public void Update_rejects_invalid_time_zone()
    {
        var policy = CreatePolicy();

        var act = () => policy.Update(
            new LocalTime(9, 0),
            new LocalTime(18, 0),
            5,
            5,
            "Not/A_Real_Zone",
            4,
            Guid.NewGuid(),
            Instant.FromUtc(2026, 1, 2, 0, 0));

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance_policy.time_zone");
    }

    [Fact]
    public void Update_applies_valid_values_and_raises_event()
    {
        var policy = CreatePolicy();
        var updatedBy = Guid.NewGuid();
        var now = Instant.FromUtc(2026, 1, 2, 0, 0);

        policy.Update(new LocalTime(8, 0), new LocalTime(17, 0), 10, 10, "Asia/Makassar", 6, updatedBy, now);

        policy.ShiftStart.Should().Be(new LocalTime(8, 0));
        policy.ShiftEnd.Should().Be(new LocalTime(17, 0));
        policy.ClockInGraceMinutes.Should().Be(10);
        policy.ClockOutGraceMinutes.Should().Be(10);
        policy.TimeZoneId.Should().Be("Asia/Makassar");
        policy.MaxIzinHours.Should().Be(6);
        policy.UpdatedByUserId.Should().Be(updatedBy);
        policy.UpdatedAtUtc.Should().Be(now);
        policy.DomainEvents.Should().ContainSingle(e => e is AttendancePolicyUpdated);
    }

    [Fact]
    public void ToAttendanceDayPolicy_maps_all_fields()
    {
        var policy = CreatePolicy();

        var mapped = policy.ToAttendanceDayPolicy();

        mapped.ShiftStart.Should().Be(policy.ShiftStart);
        mapped.ShiftEnd.Should().Be(policy.ShiftEnd);
        mapped.ClockInGraceMinutes.Should().Be(policy.ClockInGraceMinutes);
        mapped.ClockOutGraceMinutes.Should().Be(policy.ClockOutGraceMinutes);
        mapped.MaxIzinHours.Should().Be(policy.MaxIzinHours);
        mapped.TimeZone.Id.Should().Be(policy.TimeZoneId);
    }
}
