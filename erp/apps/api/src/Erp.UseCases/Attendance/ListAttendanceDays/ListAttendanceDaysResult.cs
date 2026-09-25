namespace Erp.UseCases.Attendance.ListAttendanceDays;

/// <summary>
/// One calendar date and everyone counted on it. The per-date summary is deliberately not
/// computed here — the client already holds the employee list to render the expansion, so it
/// counts what it is about to draw and the two can never disagree.
/// </summary>
public sealed class AttendanceCalendarDateResult
{
    public DateOnly Date { get; init; }

    /// <summary>False for Saturday and Sunday. No absence is reported on a non-working day.</summary>
    public bool IsWorkday { get; init; }

    /// <summary>A date after today: employees are listed, but nothing is claimed about them.</summary>
    public bool IsFuture { get; init; }

    /// <summary>
    /// Today, before the shift has closed. Statuses are still moving on their own, so the day
    /// reports arrivals (ClockedIn / NotInYet) rather than outcomes.
    /// </summary>
    public bool IsInProgress { get; init; }

    public IReadOnlyList<AttendanceDayListItemResult> Employees { get; init; } = [];
}

public sealed class ListAttendanceDaysResult
{
    /// <summary>Newest date first.</summary>
    public IReadOnlyList<AttendanceCalendarDateResult> Dates { get; init; } = [];
}
