using NodaTime;

namespace Erp.Core.Aggregates.Attendance;

/// <summary>
/// Domain-level shift policy used to derive an <see cref="AttendanceDay"/>'s status.
/// Built from the infrastructure `Attendance` configuration section.
/// </summary>
public sealed record AttendanceDayPolicy(
    LocalTime ShiftStart,
    LocalTime ShiftEnd,
    int ClockInGraceMinutes,
    int ClockOutGraceMinutes,
    DateTimeZone TimeZone);
