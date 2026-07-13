namespace Erp.UseCases.Attendance.ExportAttendanceDays;

public sealed class ExportAttendanceDayRowResult
{
    public string EmployeeFullName { get; init; } = default!;
    public string Date { get; init; } = default!;
    public string TapIn { get; init; } = default!;
    public string TapOut { get; init; } = default!;
    public string Status { get; init; } = default!;
}

public sealed class ExportAttendanceDaysResult
{
    public IReadOnlyList<ExportAttendanceDayRowResult> Rows { get; init; } = [];
}
