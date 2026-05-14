namespace Erp.UseCases.Attendance.RecordDeviceLog;

public sealed record RecordDeviceLogCommand(
    Guid EmployeeId,
    DateTimeOffset PunchedAtUtc,
    string PunchType,
    string DeviceId);
