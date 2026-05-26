using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Common;
using Wolverine;

namespace Erp.UseCases.Attendance.RecordDeviceLog;

public static class RecordDeviceLogHandler
{
    public static Task<Result<AttendanceResult>> Handle(
        RecordDeviceLogCommand command,
        IReadRepository<Employee> employees,
        IRepository<AttendanceLog> attendanceLogs,
        IMessageBus bus,
        CancellationToken ct) =>
        AttendanceLogService.RecordAsync(
            command.EmployeeId,
            command.PunchedAtUtc,
            command.PunchType,
            null,
            command.DeviceId,
            null,
            employees,
            attendanceLogs,
            bus,
            ct);
}
