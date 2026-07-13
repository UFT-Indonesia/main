namespace Erp.UseCases.Attendance.ListAttendanceDays;

public sealed class ListAttendanceDaysResult
{
    public IReadOnlyList<AttendanceDayListItemResult> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
