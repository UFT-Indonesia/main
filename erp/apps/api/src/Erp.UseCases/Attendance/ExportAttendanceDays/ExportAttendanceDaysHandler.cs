using System.Linq.Expressions;
using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using NodaTime;
using NodaTime.Text;

namespace Erp.UseCases.Attendance.ExportAttendanceDays;

/// <summary>
/// Fetches exactly the selected (employee, date) rows. Built as one OR-clause per distinct
/// employee — "employee == e AND date IN (selected dates for e)" — rather than a date-range
/// scan across all employees: a date-range scan pulls every employee's row for every day
/// between the earliest and latest selected date, even though only a handful of exact pairs
/// were asked for.
/// </summary>
internal sealed class AttendanceDayExportSpec : Specification<AttendanceDay>
{
    public AttendanceDayExportSpec(IReadOnlyCollection<(EmployeeId EmployeeId, LocalDate Date)> keys)
    {
        Query.Where(BuildKeyPredicate(keys));
        Query.Include(day => day.Employee);
        Query.OrderBy(day => day.CalendarDate).ThenBy(day => day.Employee!.FullName);
        Query.AsNoTracking();
    }

    private static Expression<Func<AttendanceDay, bool>> BuildKeyPredicate(
        IReadOnlyCollection<(EmployeeId EmployeeId, LocalDate Date)> keys)
    {
        var day = Expression.Parameter(typeof(AttendanceDay), "day");
        var employeeIdProperty = Expression.Property(day, nameof(AttendanceDay.EmployeeId));
        var calendarDateProperty = Expression.Property(day, nameof(AttendanceDay.CalendarDate));
        var containsMethod = typeof(List<LocalDate>).GetMethod(nameof(List<LocalDate>.Contains))!;

        var perEmployeeClauses = keys
            .GroupBy(key => key.EmployeeId)
            .Select(group =>
            {
                var dates = group.Select(key => key.Date).ToList();
                var employeeMatch = Expression.Equal(employeeIdProperty, Expression.Constant(group.Key));
                var dateMatch = Expression.Call(Expression.Constant(dates), containsMethod, calendarDateProperty);
                return (Expression)Expression.AndAlso(employeeMatch, dateMatch);
            })
            .Aggregate(Expression.OrElse);

        return Expression.Lambda<Func<AttendanceDay, bool>>(perEmployeeClauses, day);
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
            .Select(item => (EmployeeId: new EmployeeId(item.EmployeeId), Date: LocalDate.FromDateOnly(item.Date)))
            .ToHashSet();

        var days = await attendanceDays.ListAsync(new AttendanceDayExportSpec(keys), ct);

        var rows = days
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
