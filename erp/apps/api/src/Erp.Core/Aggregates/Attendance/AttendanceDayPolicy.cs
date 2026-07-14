using NodaTime;

namespace Erp.Core.Aggregates.Attendance;

/// <summary>
/// Domain-level shift policy used to derive an <see cref="AttendanceDay"/>'s status.
/// Built from the single global <see cref="AttendancePolicy"/> database row.
/// </summary>
public sealed record AttendanceDayPolicy(
    LocalTime ShiftStart,
    LocalTime ShiftEnd,
    int ClockInGraceMinutes,
    int ClockOutGraceMinutes,
    DateTimeZone TimeZone);
