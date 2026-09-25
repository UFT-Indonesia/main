using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using NodaTime;
using Wolverine;
using Wolverine.Attributes;

namespace Erp.UseCases.Attendance.RecordManualLog;

public static class RecordManualLogHandler
{
    /// <summary>
    /// Recomputes the day inline rather than leaving it to the queued AttendanceLogRecorded
    /// handler: the client refetches the calendar as soon as this returns, so the day has to be
    /// current before the response goes out. [Transactional] commits the punch, the recompute and
    /// the outboxed event together — a failed recompute rolls the punch back, so the error the
    /// client sees is true and a retry cannot leave a duplicate.
    /// </summary>
    [Transactional]
    public static async Task<Result<AttendanceResult>> Handle(
        RecordManualLogCommand command,
        IReadRepository<Employee> employees,
        IRepository<AttendanceLog> attendanceLogs,
        IReadRepository<AttendanceLog> attendanceLogReader,
        IRepository<AttendanceDay> attendanceDays,
        AttendanceDayPolicy policy,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct)
    {
        var result = await AttendanceLogService.RecordAsync(
            command.EmployeeId,
            command.PunchedAtUtc,
            command.PunchType,
            command.Caller.UserId,
            command.Caller.Name,
            null,
            command.Note,
            command.Caller,
            employees,
            attendanceLogs,
            clock,
            bus,
            ct);

        if (result is Result<AttendanceResult>.Success)
        {
            // The event published above still reaches the queued handler, which recomputes the
            // same day again. Harmless: AttendanceDay.Apply skips the write when nothing changed.
            await AttendanceDayRecomputeService.RecomputeAsync(
                new EmployeeId(command.EmployeeId),
                AttendanceDayRecomputeService.CalendarDateOf(Instant.FromDateTimeOffset(command.PunchedAtUtc), policy),
                attendanceLogReader,
                attendanceDays,
                policy,
                ct);
        }

        return result;
    }
}
