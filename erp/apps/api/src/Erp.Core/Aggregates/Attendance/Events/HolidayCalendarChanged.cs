using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance.Events;

/// <summary>
/// A date became, or stopped being, a holiday. Leave whose range covers it is recounted and its
/// attendance marker re-anchored. Renaming a holiday changes nothing that is counted, so it
/// raises nothing.
/// </summary>
public sealed record HolidayCalendarChanged(Guid HolidayId, LocalDate Date)
    : DomainEvent(HolidayId, nameof(Holiday), "attendance.holiday_calendar_changed");
