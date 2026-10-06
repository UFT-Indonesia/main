using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Leave;
using NodaTime;

namespace Erp.Core.Aggregates.Overtime;

/// <param name="Hours">Whole counted hours — rounded down, after grace and breaks.</param>
/// <param name="Late">The OT tap-in was beyond grace; counting starts at the actual punch.</param>
/// <param name="LeftEarly">The OT tap-out was beyond grace; counting stops at the actual punch, still paid for hours worked.</param>
/// <param name="Incomplete">No OT tap-in or no OT tap-out: pays nothing until a correction is approved.</param>
public sealed record OvertimeCount(int Hours, bool Late, bool LeftEarly, bool Incomplete);

/// <summary>The money path of GSS08: which punches an OT assignment owns, how many hours they count, what that pays.</summary>
public static class OvertimeCalculator
{
    /// <summary>
    /// Weekday: from the first <see cref="PunchType.In"/> after ShiftEnd to 05:00 next day, every
    /// punch of any type. Day off: every punch from 05:00 to 05:00 next day. Chronological.
    /// </summary>
    public static IReadOnlyList<AttendanceLog> OwnedPunches(
        OvertimeAssignment assignment, IEnumerable<AttendanceLog> punches, AttendanceDayPolicy policy)
    {
        var zone = policy.TimeZone;
        var from = assignment.Date.At(OvertimeAssignment.DayBoundary).InZoneLeniently(zone).ToInstant();
        var to = assignment.Date.PlusDays(1).At(OvertimeAssignment.DayBoundary).InZoneLeniently(zone).ToInstant();
        var inRange = punches
            .Where(p => p.EmployeeId == assignment.EmployeeId && p.PunchedAtUtc >= from && p.PunchedAtUtc < to)
            .OrderBy(p => p.PunchedAtUtc)
            .ToList();

        if (assignment.IsDayOff)
        {
            return inRange;
        }

        var shiftEnd = assignment.Date.At(policy.ShiftEnd).InZoneLeniently(zone).ToInstant();
        var first = inRange.FirstOrDefault(p => p.PunchType == PunchType.In && p.PunchedAtUtc >= shiftEnd);
        return first is null ? [] : inRange.Where(p => p.PunchedAtUtc >= first.PunchedAtUtc).ToList();
    }

    /// <summary>First owned punch is the tap-in, last is the tap-out (only when there is more than one) — same rule as AttendanceDay.</summary>
    public static (Instant? TapIn, Instant? TapOut) Taps(IReadOnlyList<AttendanceLog> owned) =>
        owned.Count == 0
            ? (null, null)
            : (owned[0].PunchedAtUtc, owned.Count > 1 ? owned[^1].PunchedAtUtc : null);

    public static OvertimeCount Count(
        OvertimeAssignment assignment, Instant? tapIn, Instant? tapOut, AttendanceDayPolicy policy)
    {
        if (tapIn is not { } tin || tapOut is not { } tout)
        {
            return new OvertimeCount(0, false, false, true);
        }

        var zone = policy.TimeZone;
        var start = assignment.StartAt(zone);
        var end = assignment.EndAt(zone);

        // Within grace the assigned edge wins; early tap-in and late tap-out are clamped to it.
        var late = tin > start.Plus(Duration.FromMinutes(policy.ClockInGraceMinutes));
        var leftEarly = tout < end.Minus(Duration.FromMinutes(policy.ClockOutGraceMinutes));
        var from = late ? tin : start;
        var to = leftEarly ? tout : end;

        var minutes = (long)(to - from).TotalMinutes;

        // Lunch comes off only when the counted span holds all of it — partial overlap deducts nothing.
        if (assignment.IsDayOff)
        {
            var lunchStart = assignment.Date.At(LeaveRequest.LunchStart).InZoneLeniently(zone).ToInstant();
            var lunchEnd = assignment.Date.At(LeaveRequest.LunchEnd).InZoneLeniently(zone).ToInstant();
            if (from <= lunchStart && to >= lunchEnd)
            {
                minutes -= 60;
            }
        }

        return new OvertimeCount((int)Math.Max(0, minutes / 60), late, leftEarly, false);
    }

    /// <summary>Per-day tier, not hours × rate: the highest tier whose minimum the day reaches.</summary>
    public static decimal Pay(int hours, IEnumerable<OvertimeTier> tiers) =>
        tiers.Where(t => t.MinHours <= hours).OrderByDescending(t => t.MinHours).FirstOrDefault()?.Amount ?? 0m;
}
