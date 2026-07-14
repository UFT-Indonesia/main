using Erp.Core.Aggregates.Attendance.Events;
using Erp.Infrastructure.Attendance;
using Hangfire;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class AttendancePolicyUpdatedHandler
{
    public static Task Handle(
        AttendancePolicyUpdated message,
        IBackgroundJobClient backgroundJobs,
        CancellationToken ct)
    {
        // Every materialized AttendanceDay row was derived under the OLD policy —
        // recompute them all under the new one. Async/non-blocking: enqueued as a
        // Hangfire job rather than run inline in this handler.
        backgroundJobs.Enqueue<RecomputeAttendanceDaysJob>(job => job.RunAsync(CancellationToken.None));
        return Task.CompletedTask;
    }
}
