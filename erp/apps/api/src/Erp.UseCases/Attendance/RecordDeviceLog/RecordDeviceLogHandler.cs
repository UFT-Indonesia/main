using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Common;

namespace Erp.UseCases.Attendance.RecordDeviceLog;

public sealed class RecordDeviceLogHandler
{
    private readonly IReadRepository<Employee> _employees;
    private readonly IRepository<AttendanceLog> _attendanceLogs;

    public RecordDeviceLogHandler(
        IReadRepository<Employee> employees,
        IRepository<AttendanceLog> attendanceLogs)
    {
        _employees = employees;
        _attendanceLogs = attendanceLogs;
    }

    public Task<Result<AttendanceResult>> Handle(
        RecordDeviceLogCommand command,
        CancellationToken ct) =>
        AttendanceLogDomainService.RecordAsync(
            command.EmployeeId,
            command.PunchedAtUtc,
            command.PunchType,
            null,
            command.DeviceId,
            null,
            _employees,
            _attendanceLogs,
            ct);
}
