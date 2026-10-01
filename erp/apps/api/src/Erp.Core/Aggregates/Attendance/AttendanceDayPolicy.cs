using NodaTime;

namespace Erp.Core.Aggregates.Attendance;

/// <summary>
/// Domain-level shift policy used to derive an <see cref="AttendanceDay"/>'s status.
/// Built from the single global <see cref="AttendancePolicy"/> database row, plus the
/// <see cref="Holiday"/> dates — so everything that already receives the policy (leave
/// counting, quota, the attendance calendar) knows which days nobody works.
/// </summary>
public sealed record AttendanceDayPolicy(
    LocalTime ShiftStart,
    LocalTime ShiftEnd,
    int ClockInGraceMinutes,
    int ClockOutGraceMinutes,
    int MaxIzinHours,
    DateTimeZone TimeZone)
{
    private static readonly IReadOnlySet<LocalDate> NoHolidays = new HashSet<LocalDate>();

    /// <summary>Every declared <see cref="Holiday"/> date. Empty unless the caller loads them.</summary>
    public IReadOnlySet<LocalDate> Holidays { get; init; } = NoHolidays;

    /// <summary>
    /// Mon–Fri and not a declared holiday. The one definition of a working day: leave charging,
    /// the "absent" rule and the calendar all ask here. countWorkdays() in leave-dialogs.tsx
    /// mirrors it on the client.
    /// </summary>
    public bool IsWorkday(LocalDate date) =>
        date.DayOfWeek is not (IsoDayOfWeek.Saturday or IsoDayOfWeek.Sunday)
        && !Holidays.Contains(date);
}
