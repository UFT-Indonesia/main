using Erp.Core.Aggregates.Leave;
using Erp.UseCases.Common;

namespace Erp.UseCases.Leave.GetBlockedLeaveDates;

/// <summary>
/// Which dates in [From, To] are blocked for one employee, given the request currently being
/// built. <paramref name="HalfDay"/>/<paramref name="HalfDayPeriod"/>/<paramref name="StartHour"/>/
/// <paramref name="EndHour"/> describe the candidate's own occupied window — see
/// <see cref="LeaveRequest.OccupiedWindow(bool, HalfDayPeriod?, int?, int?, Erp.Core.Aggregates.Attendance.AttendanceDayPolicy)"/>.
/// All blank means "not decided yet", which is priced as a full day, the one shape that
/// conflicts with everything a narrower one would too. Bounded by [From, To] on purpose: the
/// window is what keeps this from growing with tenure the way a paged list would.
/// </summary>
public sealed record GetBlockedLeaveDatesQuery(
    Guid EmployeeId,
    DateOnly From,
    DateOnly To,
    bool HalfDay,
    HalfDayPeriod? HalfDayPeriod,
    int? StartHour,
    int? EndHour,
    Caller Caller);

public sealed class BlockedLeaveDatesResult
{
    /// <summary>Genuinely conflicts with the candidate window — the picker must refuse these.</summary>
    public IReadOnlyList<DateOnly> BlockedDates { get; init; } = [];

    /// <summary>
    /// Carries an approved leave that does not conflict with the candidate window — still
    /// selectable, but worth a visual hint that part of the day is already spoken for.
    /// </summary>
    public IReadOnlyList<DateOnly> PartialDates { get; init; } = [];
}
