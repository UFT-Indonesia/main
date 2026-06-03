using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using NodaTime;

namespace Erp.UseCases.Attendance.ListAttendanceLogs;

public static class ListAttendanceLogsHandler
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static async Task<Result<ListAttendanceLogsResult>> Handle(
        ListAttendanceLogsQuery query,
        IReadRepository<AttendanceLog> attendanceLogs,
        IReadRepository<Employee> employees,
        CancellationToken ct)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(query.PageSize, MaxPageSize);

        AttendanceSource? sourceFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            if (!Enum.TryParse<AttendanceSource>(query.Source, ignoreCase: true, out var parsed))
            {
                return new Result<ListAttendanceLogsResult>.Error(
                    "attendance.source_invalid", "Source must be Device or Manual.");
            }

            sourceFilter = parsed;
        }

        PunchType? punchTypeFilter = null;
        if (!string.IsNullOrWhiteSpace(query.PunchType))
        {
            if (!Enum.TryParse<PunchType>(query.PunchType, ignoreCase: true, out var parsed))
            {
                return new Result<ListAttendanceLogsResult>.Error(
                    "attendance.punch_type_invalid", "PunchType must be In or Out.");
            }

            punchTypeFilter = parsed;
        }

        Instant? dateFrom = query.DateFrom.HasValue
            ? Instant.FromDateTimeOffset(query.DateFrom.Value)
            : null;
        Instant? dateTo = query.DateTo.HasValue
            ? Instant.FromDateTimeOffset(query.DateTo.Value)
            : null;

        // Step 1: resolve employee IDs from name search
        IReadOnlyList<Guid>? employeeIdFilter = null;
        if (!string.IsNullOrWhiteSpace(query.EmployeeSearch))
        {
            var matchedEmployees = await employees.ListAsync(
                new EmployeeNameSearchSpec(query.EmployeeSearch), ct);

            if (matchedEmployees.Count == 0)
            {
                return new Result<ListAttendanceLogsResult>.Success(new ListAttendanceLogsResult
                {
                    Items = [],
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                });
            }

            employeeIdFilter = matchedEmployees.Select(e => e.Id.Value).ToList();
        }

        // Step 2: count + paginate
        var totalCount = await attendanceLogs.CountAsync(
            new AttendanceLogListCountSpec(employeeIdFilter, dateFrom, dateTo, sourceFilter, punchTypeFilter),
            ct);

        var items = await attendanceLogs.ListAsync(
            new AttendanceLogListSpec(page, pageSize, employeeIdFilter, dateFrom, dateTo, sourceFilter, punchTypeFilter),
            ct);

        if (items.Count == 0)
        {
            return new Result<ListAttendanceLogsResult>.Success(new ListAttendanceLogsResult
            {
                Items = [],
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            });
        }

        // Step 3: batch-resolve employee names for this page
        var pageEmployeeIds = items.Select(i => i.EmployeeId.Value).Distinct().ToList();
        var pageEmployees = await employees.ListAsync(new EmployeeIdBatchSpec(pageEmployeeIds), ct);
        var nameById = pageEmployees.ToDictionary(e => e.Id.Value, e => e.FullName);

        var resultItems = items.Select(log => new AttendanceListItemResult
        {
            Id = log.Id.Value,
            EmployeeId = log.EmployeeId.Value,
            EmployeeFullName = nameById.TryGetValue(log.EmployeeId.Value, out var name) ? name : "—",
            PunchedAtUtc = log.PunchedAtUtc.ToDateTimeOffset(),
            Source = log.Source.ToString(),
            PunchType = log.PunchType.ToString(),
            DeviceId = log.DeviceId,
            RecordedByUserId = log.RecordedByUserId,
            Note = log.Note,
        }).ToList();

        return new Result<ListAttendanceLogsResult>.Success(new ListAttendanceLogsResult
        {
            Items = resultItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        });
    }
}
