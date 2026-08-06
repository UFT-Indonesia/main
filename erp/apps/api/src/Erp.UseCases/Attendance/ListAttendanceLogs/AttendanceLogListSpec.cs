using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Common;
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
        PunchType? punchType,
        Caller caller)
    {
        ApplyFilters(Query, employeeSearch, dateFrom, dateTo, source, punchType, caller);
        Query.Include(log => log.Employee);
        Query.Include(log => log.Notes);
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
        PunchType? punchType,
        Caller caller)
    {
        ApplyCallerScope(query, caller);

        if (!string.IsNullOrWhiteSpace(employeeSearch))
        {
            var needle = employeeSearch.Trim().ToLowerInvariant();
            query.Where(log => log.Employee!.FullName.ToLower().Contains(needle));
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

    /// <summary>Staff never see anyone else's punches; Owner and Manager see the whole company.</summary>
    private static void ApplyCallerScope(ISpecificationBuilder<AttendanceLog> query, Caller caller)
    {
        if (AttendanceRules.CanReadAll(caller))
        {
            return;
        }

        if (caller.EmployeeId is not { } callerEmployeeId)
        {
            query.Where(_ => false);
            return;
        }

        query.Where(log => log.EmployeeId == callerEmployeeId);
    }
}

internal sealed class AttendanceLogListCountSpec : Specification<AttendanceLog>
{
    public AttendanceLogListCountSpec(
        string? employeeSearch,
        Instant? dateFrom,
        Instant? dateTo,
        AttendanceSource? source,
        PunchType? punchType,
        Caller caller)
    {
        AttendanceLogListSpec.ApplyFilters(Query, employeeSearch, dateFrom, dateTo, source, punchType, caller);
        Query.AsNoTracking();
    }
}
