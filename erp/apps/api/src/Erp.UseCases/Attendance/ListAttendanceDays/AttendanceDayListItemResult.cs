namespace Erp.UseCases.Attendance.ListAttendanceDays;

public sealed class AttendanceDayListItemResult
{
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public DateOnly Date { get; init; }
    public DateTimeOffset? TapInUtc { get; init; }
    public DateTimeOffset? TapOutUtc { get; init; }
    public string Status { get; init; } = default!;

    /// <summary>Server-computed: whether the caller may create or alter this employee's records.</summary>
    public bool CanWrite { get; init; }
}
