using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class LeaveAttendanceSyncTests
{
    private static readonly EmployeeId Employee = new(Guid.NewGuid());
    private static readonly LeaveRequestId Request = new(Guid.NewGuid());

    private readonly IRepository<AttendanceDay> _attendanceDays = Substitute.For<IRepository<AttendanceDay>>();

    public LeaveAttendanceSyncTests()
    {
        _attendanceDays.ListAsync(Arg.Any<ISpecification<AttendanceDay>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    [Fact]
    public async Task Materialize_creates_one_row_on_the_first_day_only()
    {
        var added = CaptureAdds();

        // Thu 2026-08-20 → Wed 2026-08-26, straddling Sat 22nd and Sun 23rd.
        await LeaveAttendanceSync.MaterializeAsync(
            Request, Employee, new LocalDate(2026, 8, 20), new LocalDate(2026, 8, 26),
            _attendanceDays, CancellationToken.None);

        added.Select(day => day.CalendarDate).Should().Equal(new LocalDate(2026, 8, 20));

        added.Should().OnlyContain(day =>
            day.Status == AttendanceDayStatus.OnLeave
            && day.LeaveRequestId == Request
            && day.TapInUtc == null);
    }

    [Fact]
    public async Task Materialize_starts_on_the_first_workday_when_the_leave_opens_on_a_weekend()
    {
        var added = CaptureAdds();

        // Sat 2026-08-22 → Tue 2026-08-25: the row belongs on Monday, not the start date.
        await LeaveAttendanceSync.MaterializeAsync(
            Request, Employee, new LocalDate(2026, 8, 22), new LocalDate(2026, 8, 25),
            _attendanceDays, CancellationToken.None);

        added.Select(day => day.CalendarDate).Should().Equal(new LocalDate(2026, 8, 24));
    }

    [Fact]
    public async Task Materialize_leaves_a_day_that_already_has_punches_alone()
    {
        var punched = DayWithPunch(new LocalDate(2026, 8, 20));
        _attendanceDays.ListAsync(Arg.Any<ISpecification<AttendanceDay>>(), Arg.Any<CancellationToken>())
            .Returns([punched]);
        var added = CaptureAdds();

        await LeaveAttendanceSync.MaterializeAsync(
            Request, Employee, new LocalDate(2026, 8, 20), new LocalDate(2026, 8, 21),
            _attendanceDays, CancellationToken.None);

        // The first workday already has a row of its own, so the leave adds nothing: the day
        // is in the table on its own merits and a second row further in would be the
        // duplication this is removing.
        added.Should().BeEmpty();
        punched.Status.Should().Be(AttendanceDayStatus.Complete);
    }

    [Fact]
    public async Task Release_deletes_leave_only_rows_but_keeps_days_that_were_worked()
    {
        var leaveOnly = AttendanceDay.CreateForLeave(Employee, new LocalDate(2026, 8, 20), Request);
        var worked = DayWithPunch(new LocalDate(2026, 8, 21));
        _attendanceDays.ListAsync(Arg.Any<ISpecification<AttendanceDay>>(), Arg.Any<CancellationToken>())
            .Returns([leaveOnly, worked]);

        await LeaveAttendanceSync.ReleaseAsync(Request, _attendanceDays, CancellationToken.None);

        await _attendanceDays.Received(1).DeleteAsync(leaveOnly, Arg.Any<CancellationToken>());
        await _attendanceDays.DidNotReceive().DeleteAsync(worked, Arg.Any<CancellationToken>());
        worked.LeaveRequestId.Should().BeNull();
    }

    [Fact]
    public async Task Termination_drops_only_the_leave_days_past_the_last_working_day()
    {
        // The spec does the date/punch filtering in the database; this asserts the handler
        // deletes everything it comes back with, and nothing else.
        var afterTermination = AttendanceDay.CreateForLeave(Employee, new LocalDate(2026, 8, 27), Request);
        _attendanceDays.ListAsync(Arg.Any<ISpecification<AttendanceDay>>(), Arg.Any<CancellationToken>())
            .Returns([afterTermination]);

        await LeaveAttendanceSync.DropLeaveDaysAfterAsync(
            Employee, new LocalDate(2026, 8, 25), _attendanceDays, CancellationToken.None);

        await _attendanceDays.Received(1).DeleteAsync(afterTermination, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void The_termination_sweep_spec_takes_leave_only_days_past_the_cutoff()
    {
        var spec = new LeaveOnlyDaysAfterSpec(Employee, new LocalDate(2026, 8, 25));

        var onTheCutoff = AttendanceDay.CreateForLeave(Employee, new LocalDate(2026, 8, 25), Request);
        var afterCutoff = AttendanceDay.CreateForLeave(Employee, new LocalDate(2026, 8, 27), Request);
        var workedAfterCutoff = DayWithPunch(new LocalDate(2026, 8, 28));
        var unrelatedEmployee = AttendanceDay.CreateForLeave(
            new EmployeeId(Guid.NewGuid()), new LocalDate(2026, 8, 27), Request);

        var matched = spec.Evaluate([onTheCutoff, afterCutoff, workedAfterCutoff, unrelatedEmployee]);

        // The last working day itself is real history; a punched day is real attendance.
        matched.Should().Equal(afterCutoff);
    }

    [Fact]
    public void A_worked_day_under_leave_falls_back_to_leave_when_its_punches_go_away()
    {
        var day = AttendanceDay.CreateForLeave(Employee, new LocalDate(2026, 8, 20), Request);
        day.Recompute([Punch(day.CalendarDate, 1), Punch(day.CalendarDate, 10)], Policy);
        day.Status.Should().NotBe(AttendanceDayStatus.OnLeave);

        day.RevertToLeave();

        day.Status.Should().Be(AttendanceDayStatus.OnLeave);
        day.TapInUtc.Should().BeNull();
        day.TapOutUtc.Should().BeNull();
        day.LeaveRequestId.Should().Be(Request);
    }

    private static readonly AttendanceDayPolicy Policy = new(
        new LocalTime(8, 0),
        new LocalTime(17, 0),
        ClockInGraceMinutes: 15,
        ClockOutGraceMinutes: 15,
        DateTimeZoneProviders.Tzdb["Asia/Jakarta"]);

    private List<AttendanceDay> CaptureAdds()
    {
        var added = new List<AttendanceDay>();
        _attendanceDays.AddAsync(Arg.Do<AttendanceDay>(added.Add), Arg.Any<CancellationToken>());
        return added;
    }

    private static AttendanceDay DayWithPunch(LocalDate date)
    {
        // Inside the shift windows, so the day lands on Complete.
        var day = AttendanceDay.Create(
            Employee, date, [Punch(date, 1), Punch(date, 10)], Policy);
        return day;
    }

    private static AttendanceLog Punch(LocalDate date, int hoursAfterMidnightUtc) =>
        AttendanceLog.Manual(
            Employee,
            date.AtMidnight().InUtc().ToInstant().Plus(Duration.FromHours(hoursAfterMidnightUtc)),
            hoursAfterMidnightUtc < 5 ? PunchType.In : PunchType.Out,
            Guid.NewGuid());
}
