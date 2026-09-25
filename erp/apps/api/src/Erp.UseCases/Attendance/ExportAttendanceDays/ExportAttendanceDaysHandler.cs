using System.Text.Json;
using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.ListAttendanceDays;
using Erp.UseCases.Common.Filtering;
using NodaTime;
using NodaTime.Text;

namespace Erp.UseCases.Attendance.ExportAttendanceDays;

/// <summary>Every punch in the period, for the employees the calendar is showing.</summary>
internal sealed class AttendanceLogsInRangeSpec : Specification<AttendanceLog>
{
    public AttendanceLogsInRangeSpec(Instant start, Instant end, IReadOnlyCollection<EmployeeId> employeeIds)
    {
        Query.Where(log => log.PunchedAtUtc >= start && log.PunchedAtUtc < end
            && employeeIds.Contains(log.EmployeeId));
        Query.Include(log => log.Notes);
        Query.OrderBy(log => log.PunchedAtUtc);
        Query.AsNoTracking();
    }
}

/// <summary>
/// Exports the period as shown. Built on the same <see cref="AttendanceCalendar"/> the screen
/// renders, so an employee who never punched appears here as an Absent row rather than being
/// silently missing — which is the whole point of the calendar.
/// </summary>
public static class ExportAttendanceDaysHandler
{
    private static readonly LocalDateTimePattern LocalTimeStampPattern =
        LocalDateTimePattern.CreateWithInvariantCulture("yyyy-MM-dd HH:mm");

    public static async Task<Result<ExportAttendanceDaysResult>> Handle(
        ExportAttendanceDaysQuery query,
        IReadRepository<AttendanceDay> attendanceDays,
        IReadRepository<AttendanceLog> attendanceLogs,
        IReadRepository<Employee> employees,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        if (!AttendancePeriod.TryValidate(query.From, query.To, out var from, out var to, out var invalid))
        {
            return new Result<ExportAttendanceDaysResult>.Error(invalid.Code, invalid.Message);
        }

        if (!FilterApplier.TryCompile(
                AttendanceCalendarFilterFields.Fields, query.Filters, query.Caller, out var filters, out var failure))
        {
            return new Result<ExportAttendanceDaysResult>.Error(failure.Code, failure.Message);
        }

        // Which employees the caller may export is decided inside the calendar's employee spec,
        // the same rule the screen obeys. No separate check is needed here.
        var dates = await AttendanceCalendar.BuildAsync(
            from, to, filters, query.Caller, attendanceDays, employees, policy, clock, ct);

        if (query.ProblemsOnly)
        {
            // Same rule as the screen: a settled workday where someone is Absent or Incomplete.
            dates = dates
                .Where(date => date.IsWorkday && !date.IsFuture && !date.IsInProgress
                    && date.Employees.Any(employee =>
                        employee.Status is AttendanceCalendarStatus.Absent or AttendanceCalendarStatus.Incomplete))
                .ToList();
        }

        var windowStart = from.AtStartOfDayInZone(policy.TimeZone).ToInstant();
        var windowEnd = to.PlusDays(1).AtStartOfDayInZone(policy.TimeZone).ToInstant();
        // Only the employees actually in the file — without this a Staff export loads the whole
        // company's punches for the period just to discard all but their own.
        var employeeIds = dates
            .SelectMany(date => date.Employees)
            .Select(employee => new EmployeeId(employee.EmployeeId))
            .ToHashSet();
        var punches = await attendanceLogs.ListAsync(
            new AttendanceLogsInRangeSpec(windowStart, windowEnd, employeeIds), ct);
        var punchesByKey = punches
            .GroupBy(log => (log.EmployeeId, Date: log.PunchedAtUtc.InZone(policy.TimeZone).Date))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<AttendanceLog>)group.ToList());

        // Oldest first: a CSV is read top to bottom, unlike the screen, which leads with today.
        var rows = dates
            .OrderBy(date => date.Date)
            .SelectMany(date => date.Employees
                .OrderBy(employee => employee.EmployeeFullName, StringComparer.OrdinalIgnoreCase)
                .Select(employee => new ExportAttendanceDayRowResult
                {
                    EmployeeFullName = employee.EmployeeFullName,
                    Date = date.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    Punches = BuildPunchesJson(
                        punchesByKey.GetValueOrDefault(
                            (new EmployeeId(employee.EmployeeId), LocalDate.FromDateOnly(date.Date))) ?? [],
                        policy.TimeZone),
                    Status = employee.Status,
                    LeaveType = employee.LeaveType,
                }))
            .ToList();

        return new Result<ExportAttendanceDaysResult>.Success(
            new ExportAttendanceDaysResult { Rows = rows });
    }

    private static string BuildPunchesJson(IReadOnlyList<AttendanceLog> punches, DateTimeZone zone)
    {
        var payload = punches
            .OrderBy(log => log.PunchedAtUtc)
            .Select(log => new
            {
                time = LocalTimeStampPattern.Format(log.PunchedAtUtc.InZone(zone).LocalDateTime),
                type = log.PunchType.ToString(),
                notes = log.Notes
                    .OrderBy(note => note.CreatedAtUtc)
                    .Select(note => new
                    {
                        text = note.Text,
                        author = note.CreatedByName,
                        createdAt = LocalTimeStampPattern.Format(note.CreatedAtUtc.InZone(zone).LocalDateTime),
                    }),
            });

        return JsonSerializer.Serialize(payload);
    }
}
