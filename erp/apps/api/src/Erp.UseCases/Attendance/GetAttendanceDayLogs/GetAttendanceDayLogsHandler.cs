using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Attendance.ListAttendanceLogs;
using Erp.UseCases.Common;
using NodaTime;

namespace Erp.UseCases.Attendance.GetAttendanceDayLogs;

public static class GetAttendanceDayLogsHandler
{
    public static async Task<Result<GetAttendanceDayLogsResult>> Handle(
        GetAttendanceDayLogsQuery query,
        IReadRepository<AttendanceLog> attendanceLogs,
        AttendanceDayPolicy policy,
        CancellationToken ct)
    {
        if (!AttendanceRules.CanRead(query.Caller, new EmployeeId(query.EmployeeId)))
        {
            return new Result<GetAttendanceDayLogsResult>.Error(
                ResultErrors.Forbidden, "You cannot read another employee's attendance.");
        }

        var calendarDate = LocalDate.FromDateOnly(query.Date);
        var dayStart = calendarDate.AtStartOfDayInZone(policy.TimeZone).ToInstant();
        var dayEnd = calendarDate.PlusDays(1).AtStartOfDayInZone(policy.TimeZone).ToInstant();

        var punches = await attendanceLogs.ListAsync(
            new AttendanceLogsForEmployeeDaySpec(new EmployeeId(query.EmployeeId), dayStart, dayEnd),
            ct);

        var items = punches.Select(log => new AttendanceListItemResult
        {
            Id = log.Id.Value,
            EmployeeId = log.EmployeeId.Value,
            EmployeeFullName = log.Employee?.FullName ?? "—",
            PunchedAtUtc = log.PunchedAtUtc.ToDateTimeOffset(),
            Source = log.Source.ToString(),
            PunchType = log.PunchType.ToString(),
            DeviceId = log.DeviceId,
            RecordedByUserId = log.RecordedByUserId,
            Notes = AttendanceLogNoteResult.FromLog(log),
            CanWrite = log.Employee is not null
                && AttendanceRules.CanWriteFor(query.Caller, log.Employee),
        }).ToList();

        return new Result<GetAttendanceDayLogsResult>.Success(
            new GetAttendanceDayLogsResult { Items = items });
    }
}
