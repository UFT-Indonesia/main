using Erp.UseCases.Attendance.Common;

namespace Erp.Web.Endpoints.Attendance;

public sealed class AttendanceLogNoteResponse
{
    public Guid Id { get; init; }
    public string Text { get; init; } = default!;
    public Guid CreatedByUserId { get; init; }
    public string CreatedByName { get; init; } = default!;
    public DateTimeOffset CreatedAtUtc { get; init; }

    public static AttendanceLogNoteResponse From(AttendanceLogNoteResult note) => new()
    {
        Id = note.Id,
        Text = note.Text,
        CreatedByUserId = note.CreatedByUserId,
        CreatedByName = note.CreatedByName,
        CreatedAtUtc = note.CreatedAtUtc,
    };

    public static IReadOnlyList<AttendanceLogNoteResponse> FromAll(
        IReadOnlyList<AttendanceLogNoteResult> notes) => notes.Select(From).ToList();
}

public sealed class AddAttendanceLogNoteRequest
{
    public Guid LogId { get; init; }
    public string Text { get; init; } = default!;
}

public sealed class DeleteAttendanceLogNoteRequest
{
    public Guid LogId { get; init; }
    public Guid NoteId { get; init; }
}

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
    public IReadOnlyList<AttendanceLogNoteResponse> Notes { get; init; } = [];
}

public sealed class ListAttendanceLogsResponse
{
    public IReadOnlyList<AttendanceLogListItemResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class ListAttendanceDaysRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? EmployeeSearch { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public string? Status { get; init; }
}

public sealed class AttendanceDayListItemResponse
{
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public DateOnly Date { get; init; }
    public DateTimeOffset? TapInUtc { get; init; }
    public DateTimeOffset? TapOutUtc { get; init; }
    public string Status { get; init; } = default!;
}

public sealed class ListAttendanceDaysResponse
{
    public IReadOnlyList<AttendanceDayListItemResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class GetAttendanceDayLogsRequest
{
    public Guid EmployeeId { get; init; }
    public DateOnly Date { get; init; }
}

public sealed class GetAttendanceDayLogsResponse
{
    public IReadOnlyList<AttendanceLogListItemResponse> Items { get; init; } = [];
}

public sealed class UpdateAttendanceLogRequest
{
    public Guid Id { get; init; }
    public DateTimeOffset PunchedAtUtc { get; init; }
    public string PunchType { get; init; } = default!;
}

public sealed class ExportAttendanceDayKeyRequest
{
    public Guid EmployeeId { get; init; }
    public DateOnly Date { get; init; }
}

public sealed class ExportAttendanceDaysRequest
{
    public IReadOnlyList<ExportAttendanceDayKeyRequest> Items { get; init; } = [];
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
    public IReadOnlyList<AttendanceLogNoteResponse> Notes { get; init; } = [];
}

public sealed class AttendancePolicyResponse
{
    /// <summary>"HH:mm" formatted shift start.</summary>
    public string ShiftStart { get; init; } = default!;

    /// <summary>"HH:mm" formatted shift end.</summary>
    public string ShiftEnd { get; init; } = default!;

    public int ClockInGraceMinutes { get; init; }
    public int ClockOutGraceMinutes { get; init; }

    /// <summary>IANA time zone id (e.g. "Asia/Jakarta").</summary>
    public string TimeZoneId { get; init; } = default!;

    public Guid UpdatedByUserId { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class UpdateAttendancePolicyRequest
{
    public string ShiftStart { get; init; } = default!;
    public string ShiftEnd { get; init; } = default!;
    public int ClockInGraceMinutes { get; init; }
    public int ClockOutGraceMinutes { get; init; }
    public string TimeZoneId { get; init; } = default!;
}
