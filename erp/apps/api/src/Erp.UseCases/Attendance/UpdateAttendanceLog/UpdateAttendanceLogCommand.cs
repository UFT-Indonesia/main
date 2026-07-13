namespace Erp.UseCases.Attendance.UpdateAttendanceLog;

public sealed record UpdateAttendanceLogCommand(
    Guid LogId,
    DateTimeOffset PunchedAtUtc,
    string PunchType,
    string? Note);
