namespace Erp.UseCases.Attendance.ListAttendanceLogs;

public sealed record ListAttendanceLogsQuery(
    int Page,
    int PageSize,
    string? EmployeeSearch,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    string? PunchType,
    string? Source);
