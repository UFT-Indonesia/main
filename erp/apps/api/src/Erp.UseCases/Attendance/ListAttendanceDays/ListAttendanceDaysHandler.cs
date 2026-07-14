using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using NodaTime;

namespace Erp.UseCases.Attendance.ListAttendanceDays;

public static class ListAttendanceDaysHandler
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static async Task<Result<ListAttendanceDaysResult>> Handle(
        ListAttendanceDaysQuery query,
        IReadRepository<AttendanceDay> attendanceDays,
        CancellationToken ct)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(query.PageSize, MaxPageSize);

        AttendanceDayStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<AttendanceDayStatus>(query.Status, ignoreCase: true, out var parsed))
            {
                return new Result<ListAttendanceDaysResult>.Error(
                    "attendance.day_status_invalid", "Status must be Complete or Incomplete.");
            }

            statusFilter = parsed;
        }

        LocalDate? dateFrom = query.DateFrom.HasValue
            ? LocalDate.FromDateOnly(query.DateFrom.Value)
            : null;
        LocalDate? dateTo = query.DateTo.HasValue
            ? LocalDate.FromDateOnly(query.DateTo.Value)
            : null;

        var totalCount = await attendanceDays.CountAsync(
            new AttendanceDayListCountSpec(query.EmployeeSearch, dateFrom, dateTo, statusFilter),
            ct);

        var items = await attendanceDays.ListAsync(
            new AttendanceDayListSpec(page, pageSize, query.EmployeeSearch, dateFrom, dateTo, statusFilter),
            ct);

        var resultItems = items.Select(day => new AttendanceDayListItemResult
        {
            EmployeeId = day.EmployeeId.Value,
            EmployeeFullName = day.Employee?.FullName ?? "—",
            Date = day.CalendarDate.ToDateOnly(),
            TapInUtc = day.TapInUtc?.ToDateTimeOffset(),
            TapOutUtc = day.TapOutUtc?.ToDateTimeOffset(),
            Status = day.Status.ToString(),
        }).ToList();

        return new Result<ListAttendanceDaysResult>.Success(new ListAttendanceDaysResult
        {
            Items = resultItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        });
    }
}
