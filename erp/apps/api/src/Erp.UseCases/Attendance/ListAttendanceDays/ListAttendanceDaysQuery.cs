using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Attendance.ListAttendanceDays;

public sealed record ListAttendanceDaysQuery(
    int Page,
    int PageSize,
    IReadOnlyList<FilterRow> Filters,
    Caller Caller);
