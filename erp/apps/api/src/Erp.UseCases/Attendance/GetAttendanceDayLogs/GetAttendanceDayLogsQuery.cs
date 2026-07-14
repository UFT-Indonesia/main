namespace Erp.UseCases.Attendance.GetAttendanceDayLogs;

public sealed record GetAttendanceDayLogsQuery(Guid EmployeeId, DateOnly Date);
