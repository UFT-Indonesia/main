using NodaTime;

namespace Erp.UseCases.Attendance.ListAttendanceDays;

/// <summary>Bounds shared by the calendar list and the CSV export, so the two cannot diverge.</summary>
public static class AttendancePeriod
{
    public static bool TryValidate(
        DateOnly from,
        DateOnly to,
        out LocalDate start,
        out LocalDate end,
        out (string Code, string Message) failure)
    {
        start = LocalDate.FromDateOnly(from);
        end = LocalDate.FromDateOnly(to);

        // An omitted query parameter binds to default(DateOnly), 0001-01-01 — which would
        // otherwise pass as a valid one-day period in year 1.
        if (from == default || to == default)
        {
            failure = ("attendance.period_required", "Both the period's start and end dates are required.");
            return false;
        }

        if (end < start)
        {
            failure = ("attendance.period_inverted", "The period's end date is before its start date.");
            return false;
        }

        if (Period.DaysBetween(start, end) + 1 > AttendanceCalendar.MaxPeriodDays)
        {
            failure = (
                "attendance.period_too_long",
                $"A period cannot span more than {AttendanceCalendar.MaxPeriodDays} days.");
            return false;
        }

        failure = default;
        return true;
    }
}
