using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Leave.Common;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class LeaveAttendanceSyncTests
{
    private static readonly EmployeeId Employee = new(Guid.NewGuid());
    private static readonly LeaveRequestId Request = new(Guid.NewGuid());
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 14, 8, 0);

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
            isFractional: false, TestPolicies.Standard, _attendanceDays, CancellationToken.None);

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
            isFractional: false, TestPolicies.Standard, _attendanceDays, CancellationToken.None);

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
            isFractional: false, TestPolicies.Standard, _attendanceDays, CancellationToken.None);

        // The first workday already has a row of its own, so the leave adds nothing: the day
        // is in the table on its own merits and a second row further in would be the
        // duplication this is removing.
        added.Should().BeEmpty();
        punched.Status.Should().Be(AttendanceDayStatus.Complete);
    }

    [Fact]
    public async Task Reanchor_moves_the_row_off_a_first_day_that_became_a_holiday()
    {
        // Mon 17 → Wed 19 Aug, materialized on the 17th before HUT RI was declared.
        var request = ApprovedLeave(new LocalDate(2026, 8, 17), new LocalDate(2026, 8, 19));
        var stale = AttendanceDay.CreateForLeave(Employee, new LocalDate(2026, 8, 17), request.Id);
        _attendanceDays.ListAsync(Arg.Any<ISpecification<AttendanceDay>>(), Arg.Any<CancellationToken>())
            // Reanchor's lookup, then Release's, then Materialize's check of the new day.
            .Returns(_ => [stale], _ => [stale], _ => []);
        var added = CaptureAdds();

        await LeaveAttendanceSync.ReanchorAsync(
            request, Policy with { Holidays = new HashSet<LocalDate> { new(2026, 8, 17) } },
            _attendanceDays, CancellationToken.None);

        await _attendanceDays.Received(1).DeleteAsync(stale, Arg.Any<CancellationToken>());
        added.Select(day => day.CalendarDate).Should().Equal(new LocalDate(2026, 8, 18));
    }

    [Fact]
    public async Task Reanchor_leaves_a_row_already_on_the_first_workday_alone()
    {
        // A holiday on the 18th, mid-range, changes nothing about where the row belongs.
        var request = ApprovedLeave(new LocalDate(2026, 8, 17), new LocalDate(2026, 8, 19));
        var anchored = AttendanceDay.CreateForLeave(Employee, new LocalDate(2026, 8, 17), request.Id);
        _attendanceDays.ListAsync(Arg.Any<ISpecification<AttendanceDay>>(), Arg.Any<CancellationToken>())
            .Returns([anchored]);
        var added = CaptureAdds();

        await LeaveAttendanceSync.ReanchorAsync(
            request, Policy with { Holidays = new HashSet<LocalDate> { new(2026, 8, 18) } },
            _attendanceDays, CancellationToken.None);

        await _attendanceDays.DidNotReceive().DeleteAsync(Arg.Any<AttendanceDay>(), Arg.Any<CancellationToken>());
        added.Should().BeEmpty();
    }

    [Fact]
    public async Task Materialize_does_nothing_for_a_fractional_leave()
    {
        var added = CaptureAdds();

        // A half day or hourly Izin is quota-only — the employee works the rest of the day,
        // so attendance must stay exactly what their punches say.
        await LeaveAttendanceSync.MaterializeAsync(
            Request, Employee, new LocalDate(2026, 8, 20), new LocalDate(2026, 8, 20),
            isFractional: true, TestPolicies.Standard, _attendanceDays, CancellationToken.None);

        added.Should().BeEmpty();
        await _attendanceDays.DidNotReceive().ListAsync(
            Arg.Any<ISpecification<AttendanceDay>>(), Arg.Any<CancellationToken>());
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
    public void The_OnLeave_badge_spec_excludes_fractional_leave()
    {
        var fullDay = LeaveRequest.Create(
            Employee, LeaveType.Sick, new LocalDate(2026, 8, 20), new LocalDate(2026, 8, 20),
            "sakit", TestAttachments.DoctorsNote(), halfDay: false, halfDayPeriod: null,
            startHour: null, endHour: null, Guid.NewGuid(), Now, TestPolicies.Standard);
        fullDay.Approve(Guid.NewGuid(), "Owner Utama", Now);

        var halfDay = LeaveRequest.Create(
            Employee, LeaveType.Annual, new LocalDate(2026, 8, 20), new LocalDate(2026, 8, 20),
            "acara", null, halfDay: true, halfDayPeriod: HalfDayPeriod.Morning,
            startHour: null, endHour: null, Guid.NewGuid(), Now, TestPolicies.Standard);
        halfDay.Approve(Guid.NewGuid(), "Owner Utama", Now);

        var hourly = LeaveRequest.Create(
            Employee, LeaveType.Permission, new LocalDate(2026, 8, 20), new LocalDate(2026, 8, 20),
            "izin", null, halfDay: false, halfDayPeriod: null, startHour: 9, endHour: 11,
            Guid.NewGuid(), Now, TestPolicies.Standard);
        hourly.Approve(Guid.NewGuid(), "Owner Utama", Now);

        var spec = new FullDayApprovedLeaveOnDateSpec(Employee, new LocalDate(2026, 8, 20));

        spec.Evaluate([fullDay, halfDay, hourly]).Should().Equal(fullDay);
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
        MaxIzinHours: 4,
        DateTimeZoneProviders.Tzdb["Asia/Jakarta"]);

    private static LeaveRequest ApprovedLeave(LocalDate start, LocalDate end)
    {
        var request = LeaveRequest.Create(
            Employee, LeaveType.Annual, start, end, "liburan", null, halfDay: false, halfDayPeriod: null,
            startHour: null, endHour: null, Guid.NewGuid(), Now, Policy);
        request.Approve(Guid.NewGuid(), "Owner Utama", Now);
        return request;
    }

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
