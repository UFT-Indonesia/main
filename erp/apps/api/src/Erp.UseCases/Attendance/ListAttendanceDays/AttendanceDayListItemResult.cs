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

    /// <summary>Server-computed: whether the caller may create or alter this employee's records.</summary>
    public bool CanWrite { get; init; }
}
