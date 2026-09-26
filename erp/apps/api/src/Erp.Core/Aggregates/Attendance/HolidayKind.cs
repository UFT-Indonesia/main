namespace Erp.Core.Aggregates.Attendance;

/// <summary>
/// Display label only — both kinds close the office, spare everyone an Absent and cost no leave
/// quota. Cuti bersama is a company gift at this company, never deducted from Annual.
/// </summary>
public enum HolidayKind
{
    /// <summary>Libur nasional.</summary>
    National = 0,

    /// <summary>Cuti bersama.</summary>
    Collective = 1,
}
