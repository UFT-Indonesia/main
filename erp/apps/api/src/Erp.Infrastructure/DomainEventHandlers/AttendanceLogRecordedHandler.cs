using Erp.Core.Aggregates.Attendance.Events;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class AttendanceLogRecordedHandler
{
    public static Task Handle(
        AttendanceLogRecorded message,
        CancellationToken ct)
    {
        // TODO: Real-time dashboard push, notify supervisors, update analytics
        return Task.CompletedTask;
    }
}
