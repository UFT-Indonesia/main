using Erp.UseCases.Attendance.ListAttendanceLogs;

namespace Erp.UseCases.Attendance.GetAttendanceDayLogs;

public sealed class GetAttendanceDayLogsResult
{
    public IReadOnlyList<AttendanceListItemResult> Items { get; init; } = [];
}
