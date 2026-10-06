using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Common;
using NodaTime;

namespace Erp.UseCases.Overtime.Common;

/// <summary>An assignment read against the punches it owns: the live view, or the frozen one once its period closed.</summary>
/// <param name="Hours">Counted hours that pay. Zero unless Approved — Pending and Expired pay nothing.</param>
internal sealed record OvertimeEvaluation(
    Instant? TapIn, Instant? TapOut, OvertimeCount Count, int Hours, decimal Amount, bool HasPunches);

internal static class OvertimeEvaluator
{
    /// <summary>One query's worth of punches for every employee and date the assignments touch.</summary>
    public static async Task<IReadOnlyList<AttendanceLog>> LoadPunchesAsync(
        IReadOnlyCollection<OvertimeAssignment> assignments, IReadRepository<AttendanceLog> logs,
        AttendanceDayPolicy policy, CancellationToken ct)
    {
        if (assignments.Count == 0)
        {
            return [];
        }

        var from = assignments.Min(a => a.Date).At(OvertimeAssignment.DayBoundary).InZoneLeniently(policy.TimeZone).ToInstant();
        var to = assignments.Max(a => a.Date).PlusDays(1).At(OvertimeAssignment.DayBoundary).InZoneLeniently(policy.TimeZone).ToInstant();
        return await logs.ListAsync(
            new PunchesForEmployeesSpec(assignments.Select(a => a.EmployeeId).Distinct().ToList(), from, to), ct);
    }

    public static OvertimeEvaluation Evaluate(
        OvertimeAssignment a, IEnumerable<AttendanceLog> punches, AttendanceDayPolicy policy, IEnumerable<OvertimeTier> tiers)
    {
        // Rejected and cancelled overtime owns nothing: its punches are the regular day's again.
        if (!a.OwnsPunches)
        {
            return new OvertimeEvaluation(null, null, new OvertimeCount(0, false, false, false), 0, 0m, false);
        }

        var owned = OvertimeCalculator.OwnedPunches(a, punches, policy);
        var (tapIn, tapOut) = OvertimeCalculator.Taps(owned);
        var count = OvertimeCalculator.Count(a, tapIn, tapOut, policy);

        if (a.IsFrozen)
        {
            return new OvertimeEvaluation(tapIn, tapOut, count, a.FrozenHours ?? 0, a.FrozenAmount ?? 0m, owned.Count > 0);
        }

        var pays = a.Status == OvertimeStatus.Approved;
        return new OvertimeEvaluation(
            tapIn, tapOut, count, pays ? count.Hours : 0, pays ? OvertimeCalculator.Pay(count.Hours, tiers) : 0m, owned.Count > 0);
    }

    /// <summary>
    /// Recomputes the assignment's date and the next one: the first holds the regular-day punches
    /// that stay behind, the second the small hours the window ran into.
    /// </summary>
    public static async Task RecomputeAroundAsync(
        OvertimeAssignment a, IReadRepository<AttendanceLog> logs, IRepository<AttendanceDay> days,
        IReadRepository<OvertimeAssignment> overtime, AttendanceDayPolicy policy, CancellationToken ct)
    {
        foreach (var date in new[] { a.Date, a.Date.PlusDays(1) })
        {
            await AttendanceDayRecomputeService.RecomputeAsync(a.EmployeeId, date, logs, days, overtime, policy, ct);
        }
    }
}
