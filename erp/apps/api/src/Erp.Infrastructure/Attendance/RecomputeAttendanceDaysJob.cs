using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.Infrastructure.Persistence;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Attendance;

/// <summary>
/// Hangfire background job: recomputes every materialized <see cref="AttendanceDay"/> row
/// under the CURRENT global policy. Enqueued whenever the policy is updated. Resolves the
/// policy fresh from the database (not the request-scoped <see cref="AttendanceDayPolicy"/>)
/// since the job runs in its own DI scope, outside the HTTP request that changed it.
/// </summary>
public sealed class RecomputeAttendanceDaysJob
{
    private readonly AppDbContext _db;
    private readonly IReadRepository<AttendanceLog> _attendanceLogs;
    private readonly IRepository<AttendanceDay> _attendanceDays;

    public RecomputeAttendanceDaysJob(
        AppDbContext db,
        IReadRepository<AttendanceLog> attendanceLogs,
        IRepository<AttendanceDay> attendanceDays)
    {
        _db = db;
        _attendanceLogs = attendanceLogs;
        _attendanceDays = attendanceDays;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var policyEntity = await _db.AttendancePolicies
            .AsNoTracking()
            .SingleAsync(p => p.Id == AttendancePolicyId.Singleton, ct);
        var policy = policyEntity.ToAttendanceDayPolicy();

        var dayKeys = await _db.AttendanceDays
            .AsNoTracking()
            .Select(day => new { day.EmployeeId, day.CalendarDate })
            .Distinct()
            .ToListAsync(ct);

        foreach (var key in dayKeys)
        {
            await AttendanceDayRecomputeService.RecomputeAsync(
                key.EmployeeId, key.CalendarDate, _attendanceLogs, _attendanceDays, policy, ct);
        }
    }
}
