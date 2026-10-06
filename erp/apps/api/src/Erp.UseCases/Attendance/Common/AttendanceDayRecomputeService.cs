using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Overtime.Common;
using NodaTime;

namespace Erp.UseCases.Attendance.Common;

/// <summary>
/// Recomputes the materialized <see cref="AttendanceDay"/> row for a single
/// employee + calendar day. Called from the <c>AttendanceLogRecorded</c> domain
/// event handler and from punch-correction command handlers.
/// <para>
/// Punches claimed by an overtime assignment (<see cref="OvertimeCalculator.OwnedPunches"/>) are
/// left out: they belong to the OT, not to the regular day they happen to fall on.
/// </para>
/// </summary>
public static class AttendanceDayRecomputeService
{
    public static LocalDate CalendarDateOf(Instant punchedAtUtc, AttendanceDayPolicy policy) =>
        punchedAtUtc.InZone(policy.TimeZone).Date;

    public static async Task RecomputeAsync(
        EmployeeId employeeId,
        LocalDate calendarDate,
        IReadRepository<AttendanceLog> attendanceLogs,
        IRepository<AttendanceDay> attendanceDays,
        IReadRepository<OvertimeAssignment> overtime,
        AttendanceDayPolicy policy,
        CancellationToken ct)
    {
        var dayStart = calendarDate.AtStartOfDayInZone(policy.TimeZone).ToInstant();
        var dayEnd = calendarDate.PlusDays(1).AtStartOfDayInZone(policy.TimeZone).ToInstant();

        // An assignment dated the day before can own this day's small hours, so look back one day.
        var assignments = await overtime.ListAsync(
            new OvertimeOwningPunchesSpec(employeeId, calendarDate.PlusDays(-1), calendarDate), ct);

        var punches = await attendanceLogs.ListAsync(
            new AttendanceLogsForEmployeeDaySpec(
                employeeId, assignments.Count > 0 ? dayStart.Minus(Duration.FromDays(1)) : dayStart, dayEnd),
            ct);

        if (assignments.Count > 0)
        {
            var owned = assignments
                .SelectMany(a => OvertimeCalculator.OwnedPunches(a, punches, policy))
                .Select(p => p.Id)
                .ToHashSet();
            punches = punches.Where(p => p.PunchedAtUtc >= dayStart && !owned.Contains(p.Id)).ToList();
        }

        var existing = await attendanceDays.FirstOrDefaultAsync(
            new AttendanceDayByEmployeeDateSpec(employeeId, calendarDate),
            ct);

        if (punches.Count == 0)
        {
            // A punch was moved off this day — the derived row no longer applies, unless
            // approved leave still covers the day and holds the row up on its own.
            if (existing is null)
            {
                return;
            }

            if (existing.LeaveRequestId is null)
            {
                await attendanceDays.DeleteAsync(existing, ct);
                return;
            }

            existing.RevertToLeave();
            await attendanceDays.UpdateAsync(existing, ct);
            return;
        }

        if (existing is null)
        {
            await attendanceDays.AddAsync(
                AttendanceDay.Create(employeeId, calendarDate, punches, policy),
                ct);
            return;
        }

        existing.Recompute(punches, policy);
        await attendanceDays.UpdateAsync(existing, ct);
    }
}
