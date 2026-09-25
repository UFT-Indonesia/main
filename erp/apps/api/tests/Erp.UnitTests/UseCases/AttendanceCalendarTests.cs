using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.ListAttendanceDays;
using Erp.UseCases.Common;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

/// <summary>
/// The calendar's whole reason to exist is reporting what the database does *not* contain, so
/// these cover the derived states rather than the pass-through ones.
/// </summary>
public class AttendanceCalendarTests
{
    private readonly IReadRepository<AttendanceDay> _days = Substitute.For<IReadRepository<AttendanceDay>>();
    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();

    private static readonly DateTimeZone Zone = DateTimeZoneProviders.Tzdb["Asia/Jakarta"];

    private static readonly AttendanceDayPolicy Policy = new(
        ShiftStart: new LocalTime(7, 30),
        ShiftEnd: new LocalTime(16, 30),
        ClockInGraceMinutes: 15,
        ClockOutGraceMinutes: 15,
        MaxIzinHours: 4,
        TimeZone: Zone);

    private static readonly Caller Owner =
        new(Guid.NewGuid(), EmployeeRole.Owner, EmployeeId.New(), "Owner");

    // Wed 2 Sep 2026 is a workday; Sat 5 Sep is not.
    private static readonly LocalDate Wednesday = new(2026, 9, 2);
    private static readonly LocalDate Saturday = new(2026, 9, 5);

    private static Employee MakeEmployee(string name, LocalDate? hireDate = null)
        => Employee.Create(
            name,
            Nik.Create("3201234567890123"),
            Money.Idr(5_000_000m),
            new LocalDate(2020, 1, 1),
            EmployeeRole.Staff,
            parentId: EmployeeId.New(),
            hireDate: hireDate);

    private static IClock ClockAt(LocalDateTime local)
        => new StubClock(local.InZoneLeniently(Zone).ToInstant());

    private sealed class StubClock(Instant now) : IClock
    {
        public Instant GetCurrentInstant() => now;
    }

    private void Given(IEnumerable<Employee> employees, IEnumerable<AttendanceDay> days)
    {
        _employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(employees.ToList());
        _days.ListAsync(Arg.Any<ISpecification<AttendanceDay>>(), Arg.Any<CancellationToken>())
            .Returns(days.ToList());
    }

    private Task<IReadOnlyList<AttendanceCalendarDateResult>> Build(LocalDate from, LocalDate to, IClock clock)
        => AttendanceCalendar.BuildAsync(
            from, to, [], Owner, _days, _employees, Policy, clock, CancellationToken.None);

    [Fact]
    public async Task Employee_with_no_row_on_a_settled_workday_is_absent()
    {
        Given([MakeEmployee("Rina")], []);

        var dates = await Build(Wednesday, Wednesday, ClockAt(new LocalDateTime(2026, 9, 10, 9, 0)));

        dates.Should().ContainSingle();
        dates[0].IsWorkday.Should().BeTrue();
        dates[0].Employees.Should().ContainSingle()
            .Which.Status.Should().Be(AttendanceCalendarStatus.Absent);
    }

    [Fact]
    public async Task Nobody_is_absent_on_a_weekend()
    {
        Given([MakeEmployee("Rina")], []);

        var dates = await Build(Saturday, Saturday, ClockAt(new LocalDateTime(2026, 9, 10, 9, 0)));

        dates[0].IsWorkday.Should().BeFalse();
        dates[0].Employees.Should().BeEmpty();
    }

    [Fact]
    public async Task Somebody_hired_after_the_date_is_not_counted()
    {
        Given([MakeEmployee("Rina", hireDate: Wednesday.PlusDays(1))], []);

        var dates = await Build(Wednesday, Wednesday, ClockAt(new LocalDateTime(2026, 9, 10, 9, 0)));

        dates[0].Employees.Should().BeEmpty();
    }

    [Fact]
    public async Task Today_reports_arrivals_until_the_shift_closes()
    {
        var arrived = MakeEmployee("Zidan");
        var notYet = MakeEmployee("Rina");
        var punchedIn = AttendanceDay.Create(
            arrived.Id,
            Wednesday,
            [AttendanceLog.FromDevice(
                arrived.Id,
                new LocalDateTime(2026, 9, 2, 7, 17).InZoneLeniently(Zone).ToInstant(),
                PunchType.In,
                "DEV-01")],
            Policy);

        Given([arrived, notYet], [punchedIn]);

        // 10:00 on the day itself: nobody has clocked out yet, and that is not a failure.
        var dates = await Build(Wednesday, Wednesday, ClockAt(new LocalDateTime(2026, 9, 2, 10, 0)));

        dates[0].IsInProgress.Should().BeTrue();
        dates[0].Employees.Select(e => e.Status).Should().BeEquivalentTo(
            [AttendanceCalendarStatus.NotInYet, AttendanceCalendarStatus.ClockedIn]);
    }

    [Fact]
    public async Task Same_day_settles_once_the_shift_has_closed()
    {
        var employee = MakeEmployee("Zidan");
        var punchedIn = AttendanceDay.Create(
            employee.Id,
            Wednesday,
            [AttendanceLog.FromDevice(
                employee.Id,
                new LocalDateTime(2026, 9, 2, 7, 17).InZoneLeniently(Zone).ToInstant(),
                PunchType.In,
                "DEV-01")],
            Policy);

        Given([employee], [punchedIn]);

        // 18:00, past 16:30 plus grace: the single punch is now a genuinely missing clock-out.
        var dates = await Build(Wednesday, Wednesday, ClockAt(new LocalDateTime(2026, 9, 2, 18, 0)));

        dates[0].IsInProgress.Should().BeFalse();
        dates[0].Employees.Should().ContainSingle()
            .Which.Status.Should().Be(AttendanceCalendarStatus.Incomplete);
    }

    [Fact]
    public async Task Future_dates_claim_nothing()
    {
        Given([MakeEmployee("Rina")], []);

        var dates = await Build(Wednesday, Wednesday, ClockAt(new LocalDateTime(2026, 9, 1, 9, 0)));

        dates[0].IsFuture.Should().BeTrue();
        dates[0].Employees.Should().ContainSingle()
            .Which.Status.Should().Be(AttendanceCalendarStatus.Upcoming);
    }

    [Fact]
    public async Task Problems_sort_above_settled_rows()
    {
        var fine = MakeEmployee("Ahmad");
        var missing = MakeEmployee("Rina");
        var complete = AttendanceDay.Create(
            fine.Id,
            Wednesday,
            [
                AttendanceLog.FromDevice(fine.Id, At(7, 20), PunchType.In, "DEV-01"),
                AttendanceLog.FromDevice(fine.Id, At(16, 35), PunchType.Out, "DEV-01"),
            ],
            Policy);

        Given([fine, missing], [complete]);

        var dates = await Build(Wednesday, Wednesday, ClockAt(new LocalDateTime(2026, 9, 10, 9, 0)));

        dates[0].Employees.Select(e => e.Status).Should().Equal(
            AttendanceCalendarStatus.Absent, AttendanceCalendarStatus.Complete);

        static Instant At(int hour, int minute)
            => new LocalDateTime(2026, 9, 2, hour, minute).InZoneLeniently(Zone).ToInstant();
    }

    [Fact]
    public async Task Dates_run_newest_first()
    {
        Given([MakeEmployee("Rina")], []);

        var dates = await Build(Wednesday, Wednesday.PlusDays(2), ClockAt(new LocalDateTime(2026, 9, 10, 9, 0)));

        dates.Select(d => d.Date).Should().Equal(
            Wednesday.PlusDays(2).ToDateOnly(), Wednesday.PlusDays(1).ToDateOnly(), Wednesday.ToDateOnly());
    }
}
