namespace Erp.UseCases.Attendance.ListAttendanceDays;

/// <summary>
/// The vocabulary the calendar reports per employee-date. A superset of
/// <c>AttendanceDayStatus</c>: the three domain values pass through unchanged, and the rest
/// describe states no aggregate can hold.
/// <para>
/// <see cref="Absent"/> exists only here. AttendanceDay.Create rejects an empty punch list and
/// CreateForLeave demands a leave id, so an employee who neither punched nor was on leave has
/// no row at all — the absence is found by comparing the day's rows against who was employed,
/// never by lookup. Nothing in this file is ever written to the database.
/// </para>
/// </summary>
public static class AttendanceCalendarStatus
{
    // Straight from AttendanceDayStatus.
    public const string Complete = "Complete";
    public const string Incomplete = "Incomplete";
    public const string OnLeave = "OnLeave";

    /// <summary>Employed that day, no punch, no leave. Only ever reported for a settled workday.</summary>
    public const string Absent = "Absent";

    /// <summary>Today, before the shift has closed: has punched at least once.</summary>
    public const string ClockedIn = "ClockedIn";

    /// <summary>Today, before the shift has closed: no punch yet. Not a failure — the day is unfinished.</summary>
    public const string NotInYet = "NotInYet";

    /// <summary>A date after today. Nothing has happened, so nothing is claimed.</summary>
    public const string Upcoming = "Upcoming";

    /// <summary>
    /// Sort weight for decision 13: problems first, then alphabetical inside each group. Absent
    /// outranks Incomplete because a no-show needs chasing before a missing clock-out does.
    /// </summary>
    public static int Rank(string status) => status switch
    {
        Absent => 0,
        Incomplete => 1,
        NotInYet => 2,
        OnLeave => 3,
        ClockedIn => 4,
        Complete => 5,
        _ => 6,
    };
}
