namespace Erp.UseCases.Attendance.RecordManualLog;

public sealed record RecordManualLogCommand(
    Guid EmployeeId,
    DateTimeOffset PunchedAtUtc,
    string PunchType,
    Guid RecordedByUserId,
    string? Note);
