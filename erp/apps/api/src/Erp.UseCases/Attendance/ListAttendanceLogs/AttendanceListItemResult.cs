using Erp.UseCases.Attendance.Common;

namespace Erp.UseCases.Attendance.ListAttendanceLogs;

public sealed class AttendanceListItemResult
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public DateTimeOffset PunchedAtUtc { get; init; }
    public string Source { get; init; } = default!;
    public string PunchType { get; init; } = default!;
    public string? DeviceId { get; init; }
    public Guid? RecordedByUserId { get; init; }
    public IReadOnlyList<AttendanceLogNoteResult> Notes { get; init; } = [];
}
