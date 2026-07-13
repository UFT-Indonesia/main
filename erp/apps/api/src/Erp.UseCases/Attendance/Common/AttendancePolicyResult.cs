namespace Erp.UseCases.Attendance.Common;

public sealed class AttendancePolicyResult
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
