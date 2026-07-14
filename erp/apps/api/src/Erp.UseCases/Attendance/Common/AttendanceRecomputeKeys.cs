using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Attendance.Common;

/// <summary>Identity of one materialized <see cref="AttendanceDay"/> row: employee + calendar date.</summary>
public readonly record struct AttendanceDayKey(EmployeeId EmployeeId, LocalDate CalendarDate);

/// <summary>An employee id paired with the UTC instant of one punch, projected from the log.</summary>
public readonly record struct EmployeePunchInstant(EmployeeId EmployeeId, Instant PunchedAtUtc);

/// <summary>
/// Projects the key of every materialized <see cref="AttendanceDay"/> row. Rows are already
/// unique per (employee, date), so the result needs no further de-duplication.
/// </summary>
public sealed class AttendanceDayKeysSpec : Specification<AttendanceDay, AttendanceDayKey>
{
    public AttendanceDayKeysSpec()
    {
        Query.AsNoTracking();
        Query.Select(day => new AttendanceDayKey(day.EmployeeId, day.CalendarDate));
    }
}

/// <summary>
/// Projects (employee, punch instant) for every <see cref="AttendanceLog"/> row. Used to derive
/// which calendar days hold punches under a given time zone — the calendar date can only be
/// computed in memory (NodaTime), so this is a full projection of the log.
/// </summary>
public sealed class AllPunchInstantsSpec : Specification<AttendanceLog, EmployeePunchInstant>
{
    public AllPunchInstantsSpec()
    {
        Query.AsNoTracking();
        Query.Select(log => new EmployeePunchInstant(log.EmployeeId, log.PunchedAtUtc));
    }
}
