namespace Erp.UseCases.Attendance.ExportAttendanceDays;

public sealed class ExportAttendanceDayRowResult
{
    public string EmployeeFullName { get; init; } = default!;
    public string Date { get; init; } = default!;

    /// <summary>JSON array of the day's punches, each with its own notes (JSON-in-JSON).</summary>
    public string Punches { get; init; } = "[]";

    public string Status { get; init; } = default!;

    /// <summary>
    /// The kind of leave covering the day (Annual/Sick/…), empty when none does. The
    /// request's free-text reason is deliberately not exported — it is readable only to the
    /// employee and whoever can decide their leave, which a bulk CSV cannot enforce per row.
    /// </summary>
    public string LeaveType { get; init; } = string.Empty;
}

public sealed class ExportAttendanceDaysResult
{
    public IReadOnlyList<ExportAttendanceDayRowResult> Rows { get; init; } = [];
}
