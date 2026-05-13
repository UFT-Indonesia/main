namespace Erp.Web.Endpoints.Attendance;

public sealed record DeviceAttendanceLogRequest(Guid EmployeeId, DateTimeOffset PunchedAtUtc, string PunchType, string DeviceId);

public sealed record ManualAttendanceLogRequest(Guid EmployeeId, DateTimeOffset PunchedAtUtc, string PunchType, string? Note);

public sealed record AttendanceLogResponse(
    Guid Id,
    Guid EmployeeId,
    DateTimeOffset PunchedAtUtc,
    string Source,
    string PunchType,
    string? DeviceId,
    Guid? RecordedByUserId,
    string? Note);
