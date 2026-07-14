using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;

namespace Erp.Infrastructure.Attendance;

/// <summary>
/// Hangfire background job: recomputes every materialized <see cref="AttendanceDay"/> row
/// under the CURRENT global policy. Enqueued whenever the policy is updated. Resolves the
/// policy fresh from the database (not the request-scoped <see cref="AttendanceDayPolicy"/>)
/// since the job runs in its own DI scope, outside the HTTP request that changed it.
/// </summary>
public sealed class RecomputeAttendanceDaysJob
{
    private readonly IReadRepository<AttendancePolicy> _policies;
    private readonly IReadRepository<AttendanceLog> _attendanceLogs;
    private readonly IRepository<AttendanceDay> _attendanceDays;

    public RecomputeAttendanceDaysJob(
        IReadRepository<AttendancePolicy> policies,
        IReadRepository<AttendanceLog> attendanceLogs,
        IRepository<AttendanceDay> attendanceDays)
    {
        _policies = policies;
        _attendanceLogs = attendanceLogs;
        _attendanceDays = attendanceDays;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var policyEntity = await _policies.GetByIdAsync(AttendancePolicyId.Singleton, ct)
            ?? throw new InvalidOperationException("Attendance policy singleton row is missing.");
        var policy = policyEntity.ToAttendanceDayPolicy();

        // Recompute the UNION of (a) days that already have a materialized row and (b) days that
        // hold punches under the NEW policy's time zone. If TimeZoneId changed, a punch can move
        // to a different calendar date: (a) alone would delete the now-empty old row without ever
        // creating the new date's row (silent data loss). RecomputeAsync handles each key in the
        // right direction — create, update, or delete.
        //
        // TODO(perf): this scans the whole AttendanceLog + AttendanceDay tables on every policy
        // change. Cheap while the punch log is small; page by employee (or bound by date) if the
        // log ever grows into the millions of rows.
        var punchInstants = await _attendanceLogs.ListAsync(new AllPunchInstantsSpec(), ct);
        var existingDayKeys = await _attendanceDays.ListAsync(new AttendanceDayKeysSpec(), ct);

        var keys = new HashSet<AttendanceDayKey>(existingDayKeys);
        foreach (var punch in punchInstants)
        {
            keys.Add(new AttendanceDayKey(
                punch.EmployeeId,
                AttendanceDayRecomputeService.CalendarDateOf(punch.PunchedAtUtc, policy)));
        }

        foreach (var key in keys)
        {
            await AttendanceDayRecomputeService.RecomputeAsync(
                key.EmployeeId, key.CalendarDate, _attendanceLogs, _attendanceDays, policy, ct);
        }
    }
}
