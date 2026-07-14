using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using NodaTime;

namespace Erp.UseCases.Attendance.UpdateAttendanceLog;

public static class UpdateAttendanceLogHandler
{
    public static async Task<Result<AttendanceResult>> Handle(
        UpdateAttendanceLogCommand command,
        IRepository<AttendanceLog> attendanceLogs,
        IReadRepository<AttendanceLog> attendanceLogReader,
        IRepository<AttendanceDay> attendanceDays,
        AttendanceDayPolicy policy,
        CancellationToken ct)
    {
        if (!Enum.TryParse<PunchType>(command.PunchType, ignoreCase: true, out var punchType))
        {
            return new Result<AttendanceResult>.Error(
                "attendance.punch_type", "Punch type must be In or Out.");
        }

        var log = await attendanceLogs.GetByIdAsync(new AttendanceLogId(command.LogId), ct);
        if (log is null)
        {
            return new Result<AttendanceResult>.NotFound("Attendance log was not found.");
        }

        var newPunchedAt = Instant.FromDateTimeOffset(command.PunchedAtUtc);
        var oldDate = AttendanceDayRecomputeService.CalendarDateOf(log.PunchedAtUtc, policy);
        var newDate = AttendanceDayRecomputeService.CalendarDateOf(newPunchedAt, policy);

        log.UpdateManualEntry(newPunchedAt, punchType, command.Note);
        await attendanceLogs.UpdateAsync(log, ct);

        // The derived Tap-In/Tap-Out/Status may now be stale — recompute the
        // affected day(s). Editing the timestamp can move the punch across days.
        await AttendanceDayRecomputeService.RecomputeAsync(
            log.EmployeeId, oldDate, attendanceLogReader, attendanceDays, policy, ct);
        if (newDate != oldDate)
        {
            await AttendanceDayRecomputeService.RecomputeAsync(
                log.EmployeeId, newDate, attendanceLogReader, attendanceDays, policy, ct);
        }

        return new Result<AttendanceResult>.Success(new AttendanceResult
        {
            Id = log.Id.Value,
            EmployeeId = log.EmployeeId.Value,
            PunchedAtUtc = log.PunchedAtUtc.ToDateTimeOffset(),
            Source = log.Source.ToString(),
            PunchType = log.PunchType.ToString(),
            DeviceId = log.DeviceId,
            RecordedByUserId = log.RecordedByUserId,
            Note = log.Note,
        });
    }
}
