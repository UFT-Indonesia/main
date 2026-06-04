using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using NodaTime;

namespace Erp.UseCases.Attendance.ListAttendanceLogs;

internal sealed class AttendanceLogListSpec : Specification<AttendanceLog>
{
    public AttendanceLogListSpec(
        int page,
        int pageSize,
        string? employeeSearch,
        Instant? dateFrom,
        Instant? dateTo,
        AttendanceSource? source,
        PunchType? punchType)
    {
        ApplyFilters(Query, employeeSearch, dateFrom, dateTo, source, punchType);
        Query.Include(log => log.Employee);
        Query.OrderByDescending(log => log.PunchedAtUtc);
        Query.AsNoTracking();
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }

    internal static void ApplyFilters(
        ISpecificationBuilder<AttendanceLog> query,
        string? employeeSearch,
        Instant? dateFrom,
        Instant? dateTo,
        AttendanceSource? source,
        PunchType? punchType)
    {
        if (!string.IsNullOrWhiteSpace(employeeSearch))
        {
            query.Where(log => log.Employee!.FullName.Contains(employeeSearch));
        }

        if (dateFrom.HasValue)
        {
            query.Where(log => log.PunchedAtUtc >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query.Where(log => log.PunchedAtUtc < dateTo.Value);
        }

        if (source.HasValue)
        {
            query.Where(log => log.Source == source.Value);
        }

        if (punchType.HasValue)
        {
            query.Where(log => log.PunchType == punchType.Value);
        }
    }
}

internal sealed class AttendanceLogListCountSpec : Specification<AttendanceLog>
{
    public AttendanceLogListCountSpec(
        string? employeeSearch,
        Instant? dateFrom,
        Instant? dateTo,
        AttendanceSource? source,
        PunchType? punchType)
    {
        AttendanceLogListSpec.ApplyFilters(Query, employeeSearch, dateFrom, dateTo, source, punchType);
        Query.AsNoTracking();
    }
}
