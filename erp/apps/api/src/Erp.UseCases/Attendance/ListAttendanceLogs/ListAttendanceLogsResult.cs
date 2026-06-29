namespace Erp.UseCases.Attendance.ListAttendanceLogs;

public sealed class ListAttendanceLogsResult
{
    public IReadOnlyList<AttendanceListItemResult> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
