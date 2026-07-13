using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.Domain;

public class AttendanceDayTests
{
    // Shift 09:00-18:00 WIB (UTC+7), 5 minutes grace on both sides.
    private static readonly AttendanceDayPolicy Policy = new(
        new LocalTime(9, 0),
        new LocalTime(18, 0),
        ClockInGraceMinutes: 5,
        ClockOutGraceMinutes: 5,
        DateTimeZoneProviders.Tzdb["Asia/Jakarta"]);

    private static readonly LocalDate Date = new(2026, 6, 1);

    // 09:00 WIB == 02:00 UTC; 18:00 WIB == 11:00 UTC.
    private static Instant Wib(int hour, int minute) =>
        Date.At(new LocalTime(hour, minute)).InZoneStrictly(Policy.TimeZone).ToInstant();

    private static AttendanceLog Punch(EmployeeId employeeId, Instant at, PunchType type) =>
        AttendanceLog.FromDevice(employeeId, at, type, "esp32-1");

    private static AttendanceDay CreateDay(EmployeeId employeeId, params AttendanceLog[] punches) =>
        AttendanceDay.Create(employeeId, Date, punches, Policy);

    [Fact]
    public void Create_with_on_time_in_and_out_is_complete()
    {
        var employeeId = EmployeeId.New();

        var day = CreateDay(
            employeeId,
            Punch(employeeId, Wib(8, 55), PunchType.In),
            Punch(employeeId, Wib(18, 10), PunchType.Out));

        day.EmployeeId.Should().Be(employeeId);
        day.CalendarDate.Should().Be(Date);
        day.TapInUtc.Should().Be(Wib(8, 55));
        day.TapOutUtc.Should().Be(Wib(18, 10));
        day.Status.Should().Be(AttendanceDayStatus.Complete);
        day.DomainEvents.Should().ContainSingle(e => e is AttendanceDayRecomputed);
    }

    [Fact]
    public void Late_arrival_is_incomplete()
    {
        var employeeId = EmployeeId.New();

        var day = CreateDay(
            employeeId,
            Punch(employeeId, Wib(9, 6), PunchType.In),
            Punch(employeeId, Wib(18, 30), PunchType.Out));

        day.Status.Should().Be(AttendanceDayStatus.Incomplete);
    }

    [Fact]
    public void Early_departure_is_incomplete()
    {
        var employeeId = EmployeeId.New();

        var day = CreateDay(
            employeeId,
            Punch(employeeId, Wib(8, 30), PunchType.In),
            Punch(employeeId, Wib(17, 54), PunchType.Out));

        day.Status.Should().Be(AttendanceDayStatus.Incomplete);
    }

    [Fact]
    public void Single_punch_has_no_tap_out_and_is_incomplete()
    {
        var employeeId = EmployeeId.New();

        var day = CreateDay(employeeId, Punch(employeeId, Wib(8, 45), PunchType.In));

        day.TapInUtc.Should().Be(Wib(8, 45));
        day.TapOutUtc.Should().BeNull();
        day.Status.Should().Be(AttendanceDayStatus.Incomplete);
    }

    [Fact]
    public void Punches_exactly_at_grace_edges_are_complete()
    {
        var employeeId = EmployeeId.New();

        var day = CreateDay(
            employeeId,
            Punch(employeeId, Wib(9, 5), PunchType.In),
            Punch(employeeId, Wib(17, 55), PunchType.Out));

        day.Status.Should().Be(AttendanceDayStatus.Complete);
    }

    [Fact]
    public void Punches_one_minute_past_grace_edges_are_incomplete()
    {
        var employeeId = EmployeeId.New();

        var day = CreateDay(
            employeeId,
            Punch(employeeId, Wib(9, 6), PunchType.In),
            Punch(employeeId, Wib(18, 10), PunchType.Out));

        day.Status.Should().Be(AttendanceDayStatus.Incomplete);
    }

    [Fact]
    public void Recompute_uses_first_and_last_punch_and_ignores_intermediate_ones()
    {
        var employeeId = EmployeeId.New();
        var day = CreateDay(employeeId, Punch(employeeId, Wib(8, 50), PunchType.In));
        day.ClearDomainEvents();

        day.Recompute(
            [
                Punch(employeeId, Wib(8, 50), PunchType.In),
                Punch(employeeId, Wib(12, 0), PunchType.Out),
                Punch(employeeId, Wib(12, 45), PunchType.In),
                Punch(employeeId, Wib(18, 5), PunchType.Out),
            ],
            Policy);

        day.TapInUtc.Should().Be(Wib(8, 50));
        day.TapOutUtc.Should().Be(Wib(18, 5));
        day.Status.Should().Be(AttendanceDayStatus.Complete);
        day.DomainEvents.Should().ContainSingle(e => e is AttendanceDayRecomputed);
    }

    [Fact]
    public void Recompute_is_a_no_op_when_nothing_changed()
    {
        var employeeId = EmployeeId.New();
        var punches = new[]
        {
            Punch(employeeId, Wib(8, 55), PunchType.In),
            Punch(employeeId, Wib(18, 10), PunchType.Out),
        };
        var day = CreateDay(employeeId, punches);
        day.ClearDomainEvents();

        day.Recompute(punches, Policy);

        day.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Recompute_requires_at_least_one_punch()
    {
        var employeeId = EmployeeId.New();
        var day = CreateDay(employeeId, Punch(employeeId, Wib(8, 55), PunchType.In));

        var act = () => day.Recompute([], Policy);

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance_day.no_punches");
    }

    [Fact]
    public void Create_requires_employee_id()
    {
        var employeeId = EmployeeId.New();
        var act = () => AttendanceDay.Create(
            EmployeeId.Empty,
            Date,
            [Punch(employeeId, Wib(8, 55), PunchType.In)],
            Policy);

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance_day.employee_id");
    }
}
