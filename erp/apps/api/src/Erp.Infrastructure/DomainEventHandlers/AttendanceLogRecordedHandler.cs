using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class AttendanceLogRecordedHandler
{
    public static async Task Handle(
        AttendanceLogRecorded message,
        IReadRepository<AttendanceLog> attendanceLogs,
        IRepository<AttendanceDay> attendanceDays,
        AttendanceDayPolicy policy,
        CancellationToken ct)
    {
        // Keep the materialized employee-day view in sync with the new punch.
        var calendarDate = AttendanceDayRecomputeService.CalendarDateOf(message.PunchedAtUtc, policy);

        await AttendanceDayRecomputeService.RecomputeAsync(
            new EmployeeId(message.EmployeeId),
            calendarDate,
            attendanceLogs,
            attendanceDays,
            policy,
            ct);

        // TODO: Real-time dashboard push, notify supervisors, update analytics
    }
}
