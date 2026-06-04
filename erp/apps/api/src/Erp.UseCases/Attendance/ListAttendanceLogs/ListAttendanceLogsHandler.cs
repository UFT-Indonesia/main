using Erp.Core.Aggregates.Attendance;
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

        // Step 1: count + paginate (employee name filter pushed into spec as JOIN)
        var totalCount = await attendanceLogs.CountAsync(
            new AttendanceLogListCountSpec(query.EmployeeSearch, dateFrom, dateTo, sourceFilter, punchTypeFilter),
            ct);

        var items = await attendanceLogs.ListAsync(
            new AttendanceLogListSpec(page, pageSize, query.EmployeeSearch, dateFrom, dateTo, sourceFilter, punchTypeFilter),
            ct);

        var resultItems = items.Select(log => new AttendanceListItemResult
        {
            Id = log.Id.Value,
            EmployeeId = log.EmployeeId.Value,
            EmployeeFullName = log.Employee?.FullName ?? "—",
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
