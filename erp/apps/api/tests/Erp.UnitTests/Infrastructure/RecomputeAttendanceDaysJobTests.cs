using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.Infrastructure.Attendance;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.Infrastructure;

public class RecomputeAttendanceDaysJobTests
{
    private readonly IReadRepository<AttendancePolicy> _policies =
        Substitute.For<IReadRepository<AttendancePolicy>>();
    private readonly IReadRepository<AttendanceLog> _logs =
        Substitute.For<IReadRepository<AttendanceLog>>();
    private readonly IRepository<AttendanceDay> _days =
        Substitute.For<IRepository<AttendanceDay>>();

    /// <summary>
    /// Regression: when the policy's time zone changes, a punch can move to a different calendar
    /// date. The job must CREATE the new date's row (derived from the punch under the new zone),
    /// not merely delete the now-empty old row. Enumerating only existing AttendanceDay keys
    /// (the pre-fix behavior) would delete the old row and silently drop the new day.
    /// </summary>
    [Fact]
    public async Task Run_time_zone_change_creates_moved_day_and_deletes_emptied_day()
    {
        var employee = EmployeeId.New();

        // Punch at 18:00 UTC: calendar date 2026-01-10 under UTC (the OLD zone),
        // but 2026-01-11 01:00 under Asia/Jakarta (UTC+7, the NEW zone) -> date flips forward.
        var punchInstant = Instant.FromUtc(2026, 1, 10, 18, 0);
        var punch = AttendanceLog.FromDevice(employee, punchInstant, PunchType.In, "device-1");
        var allLogs = new List<AttendanceLog> { punch };

        var newPolicyEntity = AttendancePolicy.Create(
            new LocalTime(9, 0), new LocalTime(18, 0), 5, 5, "Asia/Jakarta",
            Guid.Empty, Instant.FromUtc(2026, 1, 1, 0, 0));
        var newPolicy = newPolicyEntity.ToAttendanceDayPolicy();

        var oldDate = new LocalDate(2026, 1, 10);
        var newDate = new LocalDate(2026, 1, 11);

        // Sanity: the real conversion actually moves the punch forward a day under the new zone.
        AttendanceDayRecomputeService.CalendarDateOf(punchInstant, newPolicy).Should().Be(newDate);

        // The materialized row that exists today, derived under the OLD (UTC) zone.
        var existingDay = AttendanceDay.Create(employee, oldDate, allLogs, newPolicy);
        var existingDays = new List<AttendanceDay> { existingDay };

        _policies.GetByIdAsync(AttendancePolicyId.Singleton, Arg.Any<CancellationToken>())
            .Returns(newPolicyEntity);

        // Apply the real specs against the in-memory data so the time-zone window math runs for real.
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog, EmployeePunchInstant>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ISpecification<AttendanceLog, EmployeePunchInstant>>().Evaluate(allLogs).ToList());
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ISpecification<AttendanceLog>>().Evaluate(allLogs).ToList());

        _days.ListAsync(Arg.Any<ISpecification<AttendanceDay, AttendanceDayKey>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ISpecification<AttendanceDay, AttendanceDayKey>>().Evaluate(existingDays).ToList());
        _days.FirstOrDefaultAsync(Arg.Any<ISpecification<AttendanceDay>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ISpecification<AttendanceDay>>().Evaluate(existingDays).FirstOrDefault());

        var job = new RecomputeAttendanceDaysJob(_policies, _logs, _days);
        await job.RunAsync(CancellationToken.None);

        // New date's row is materialized from the moved punch...
        await _days.Received(1).AddAsync(
            Arg.Is<AttendanceDay>(d => d.EmployeeId == employee && d.CalendarDate == newDate),
            Arg.Any<CancellationToken>());

        // ...and the now-empty old-date row is removed.
        await _days.Received(1).DeleteAsync(existingDay, Arg.Any<CancellationToken>());
    }
}
