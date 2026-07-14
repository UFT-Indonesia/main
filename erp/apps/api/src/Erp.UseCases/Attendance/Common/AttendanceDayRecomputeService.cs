using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Attendance.Common;

/// <summary>
/// Recomputes the materialized <see cref="AttendanceDay"/> row for a single
/// employee + calendar day. Called from the <c>AttendanceLogRecorded</c> domain
/// event handler and from punch-correction command handlers.
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
        AttendanceDayPolicy policy,
        CancellationToken ct)
    {
        var dayStart = calendarDate.AtStartOfDayInZone(policy.TimeZone).ToInstant();
        var dayEnd = calendarDate.PlusDays(1).AtStartOfDayInZone(policy.TimeZone).ToInstant();

        var punches = await attendanceLogs.ListAsync(
            new AttendanceLogsForEmployeeDaySpec(employeeId, dayStart, dayEnd),
            ct);

        var existing = await attendanceDays.FirstOrDefaultAsync(
            new AttendanceDayByEmployeeDateSpec(employeeId, calendarDate),
            ct);

        if (punches.Count == 0)
        {
            // A punch was moved off this day — the derived row no longer applies.
            if (existing is not null)
            {
                await attendanceDays.DeleteAsync(existing, ct);
            }

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
