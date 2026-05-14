using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Common;

namespace Erp.UseCases.Attendance.RecordManualLog;

public sealed class RecordManualLogHandler
{
    private readonly IReadRepository<Employee> _employees;
    private readonly IRepository<AttendanceLog> _attendanceLogs;

    public RecordManualLogHandler(
        IReadRepository<Employee> employees,
        IRepository<AttendanceLog> attendanceLogs)
    {
        _employees = employees;
        _attendanceLogs = attendanceLogs;
    }

    public Task<Result<AttendanceResult>> Handle(
        RecordManualLogCommand command,
        CancellationToken ct) =>
        AttendanceLogDomainService.RecordAsync(
            command.EmployeeId,
            command.PunchedAtUtc,
            command.PunchType,
            command.RecordedByUserId,
            null,
            command.Note,
            _employees,
            _attendanceLogs,
            ct);
}
