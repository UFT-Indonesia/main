namespace Erp.UseCases.Attendance;

public sealed record RecordDeviceAttendanceLog(
    Guid EmployeeId,
    DateTimeOffset PunchedAtUtc,
    string PunchType,
    string DeviceId);
