using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Attendance.ListAttendanceDays;

/// <summary>
/// A calendar window, inclusive at both ends. There is no paging: the period bounds the size,
/// and the client expands a date without another request.
/// </summary>
public sealed record ListAttendanceDaysQuery(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<FilterRow> Filters,
    Caller Caller);
