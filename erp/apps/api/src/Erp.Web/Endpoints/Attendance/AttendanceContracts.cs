namespace Erp.Web.Endpoints.Attendance;

public sealed class DeviceAttendanceLogRequest
{
    public Guid EmployeeId { get; init; }
    public DateTimeOffset PunchedAtUtc { get; init; }
    public string PunchType { get; init; } = default!;
    public string DeviceId { get; init; } = default!;
}

public sealed class ManualAttendanceLogRequest
{
    public Guid EmployeeId { get; init; }
    public DateTimeOffset PunchedAtUtc { get; init; }
    public string PunchType { get; init; } = default!;
    public string? Note { get; init; }
}

public sealed class AttendanceLogResponse
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public DateTimeOffset PunchedAtUtc { get; init; }
    public string Source { get; init; } = default!;
    public string PunchType { get; init; } = default!;
    public string? DeviceId { get; init; }
    public Guid? RecordedByUserId { get; init; }
    public string? Note { get; init; }
}
