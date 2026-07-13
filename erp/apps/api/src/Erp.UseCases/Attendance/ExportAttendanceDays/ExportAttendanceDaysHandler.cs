using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using NodaTime;
using NodaTime.Text;

namespace Erp.UseCases.Attendance.ExportAttendanceDays;

internal sealed class AttendanceDayExportSpec : Specification<AttendanceDay>
{
    public AttendanceDayExportSpec(LocalDate minDate, LocalDate maxDate)
    {
        Query.Where(day => day.CalendarDate >= minDate && day.CalendarDate <= maxDate);
        Query.Include(day => day.Employee);
        Query.OrderBy(day => day.CalendarDate).ThenBy(day => day.Employee!.FullName);
        Query.AsNoTracking();
    }
}

public static class ExportAttendanceDaysHandler
{
    private const int MaxKeys = 500;

    private static readonly LocalDateTimePattern LocalTimeStampPattern =
        LocalDateTimePattern.CreateWithInvariantCulture("yyyy-MM-dd HH:mm");

    public static async Task<Result<ExportAttendanceDaysResult>> Handle(
        ExportAttendanceDaysQuery query,
        IReadRepository<AttendanceDay> attendanceDays,
        AttendanceDayPolicy policy,
        CancellationToken ct)
    {
        if (query.Items is not { Count: > 0 })
        {
            return new Result<ExportAttendanceDaysResult>.Error(
                "attendance.export_empty", "Select at least one attendance day to export.");
        }

        if (query.Items.Count > MaxKeys)
        {
            return new Result<ExportAttendanceDaysResult>.Error(
                "attendance.export_too_many", $"Cannot export more than {MaxKeys} rows at once.");
        }

        var keys = query.Items
            .Select(item => (item.EmployeeId, Date: LocalDate.FromDateOnly(item.Date)))
            .ToHashSet();

        var minDate = keys.Min(key => key.Date);
        var maxDate = keys.Max(key => key.Date);

        // Fetch the date-range superset, then narrow to the exact selected
        // (employee, day) pairs in memory — tuple IN is not translatable.
        var candidates = await attendanceDays.ListAsync(
            new AttendanceDayExportSpec(minDate, maxDate),
            ct);

        var rows = candidates
            .Where(day => keys.Contains((day.EmployeeId.Value, day.CalendarDate)))
            .Select(day => new ExportAttendanceDayRowResult
            {
                EmployeeFullName = day.Employee?.FullName ?? "—",
                Date = day.CalendarDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                TapIn = FormatLocal(day.TapInUtc, policy.TimeZone),
                TapOut = FormatLocal(day.TapOutUtc, policy.TimeZone),
                Status = day.Status.ToString(),
            })
            .ToList();

        return new Result<ExportAttendanceDaysResult>.Success(
            new ExportAttendanceDaysResult { Rows = rows });
    }

    private static string FormatLocal(Instant? instant, DateTimeZone zone) =>
        instant.HasValue
            ? LocalTimeStampPattern.Format(instant.Value.InZone(zone).LocalDateTime)
            : string.Empty;
}
