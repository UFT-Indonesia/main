using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance.Events;

/// <summary>
/// Emitted whenever the derived employee-day view (Tap-In / Tap-Out / Status)
/// changes because punches were recorded or corrected.
/// </summary>
public sealed record AttendanceDayRecomputed(
    Guid DayId,
    Guid EmployeeId,
    LocalDate CalendarDate,
    Instant? TapInUtc,
    Instant? TapOutUtc,
    AttendanceDayStatus Status)
    : DomainEvent(DayId, nameof(AttendanceDay), "attendance.day_recomputed");
