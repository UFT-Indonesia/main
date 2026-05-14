namespace Erp.UseCases.Attendance.Common;

public sealed class AttendanceResult
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
