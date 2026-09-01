namespace Erp.UseCases.Attendance.ListAttendanceDays;

public sealed class AttendanceDayListItemResult
{
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public DateOnly Date { get; init; }
    public DateTimeOffset? TapInUtc { get; init; }
    public DateTimeOffset? TapOutUtc { get; init; }
    public string Status { get; init; } = default!;

    /// <summary>
    /// The kind of leave covering this day (Annual/Sick/…), empty when none does. Set even
    /// when Status is Complete/Incomplete — a punch outranks the leave for status, but the
    /// day stays attributable to it (see <c>AttendanceDay.LeaveRequestId</c>).
    /// </summary>
    public string LeaveType { get; init; } = string.Empty;

    /// <summary>
    /// Detail of the leave covering this day, all null when none does. Denormalized off the
    /// already-Include'd LeaveRequest navigation (see AttendanceDayListSpec), so this costs no
    /// extra query.
    /// </summary>
    /// <remarks>
    /// Reason needs no per-row permission check of its own: AttendanceDayListSpec.ApplyCallerScope
    /// already restricts Staff to their own days and lets only Owner/Manager see everyone's, which
    /// is exactly the rule this reason is meant to follow. Any row a caller can fetch is a row
    /// whose reason they may read.
    /// </remarks>
    public DateOnly? LeaveStartDate { get; init; }
    public DateOnly? LeaveEndDate { get; init; }
    public int? LeaveWorkdayCount { get; init; }
    public string? LeaveReason { get; init; }
    public DateTimeOffset? LeaveRequestedAtUtc { get; init; }
    public string? LeaveDecidedByName { get; init; }
    public DateTimeOffset? LeaveDecidedAtUtc { get; init; }

    /// <summary>Server-computed: whether the caller may create or alter this employee's records.</summary>
    public bool CanWrite { get; init; }
}
