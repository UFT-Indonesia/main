namespace Erp.UseCases.Attendance;

public sealed record RecordManualAttendanceLog(
    Guid EmployeeId,
    DateTimeOffset PunchedAtUtc,
    string PunchType,
    Guid RecordedByUserId,
    string? Note);
