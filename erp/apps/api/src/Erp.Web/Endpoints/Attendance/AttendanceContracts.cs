namespace Erp.Web.Endpoints.Attendance;

public sealed class ListAttendanceLogsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? EmployeeSearch { get; init; }
    public DateTimeOffset? DateFrom { get; init; }
    public DateTimeOffset? DateTo { get; init; }
    public string? PunchType { get; init; }
    public string? Source { get; init; }
}

public sealed class AttendanceLogListItemResponse
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public DateTimeOffset PunchedAtUtc { get; init; }
    public string Source { get; init; } = default!;
    public string PunchType { get; init; } = default!;
    public string? DeviceId { get; init; }
    public Guid? RecordedByUserId { get; init; }
    public string? Note { get; init; }
}

public sealed class ListAttendanceLogsResponse
{
    public IReadOnlyList<AttendanceLogListItemResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

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
