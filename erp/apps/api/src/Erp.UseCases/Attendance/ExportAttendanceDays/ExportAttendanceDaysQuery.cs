using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Attendance.ExportAttendanceDays;

/// <summary>
/// The period as shown, with the same employee filters the calendar is displaying, so the file
/// and the screen always agree.
/// </summary>
public sealed record ExportAttendanceDaysQuery(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<FilterRow> Filters,
    Caller Caller,
    bool ProblemsOnly = false);
