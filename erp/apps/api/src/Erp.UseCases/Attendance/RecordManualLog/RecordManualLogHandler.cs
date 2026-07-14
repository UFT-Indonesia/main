using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Attendance.RecordManualLog;

public static class RecordManualLogHandler
{
    public static Task<Result<AttendanceResult>> Handle(
        RecordManualLogCommand command,
        IReadRepository<Employee> employees,
        IRepository<AttendanceLog> attendanceLogs,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct) =>
        // TODO: Enforce RBS permission check — only authorized roles should be able to record manual logs for arbitrary employees.
        AttendanceLogService.RecordAsync(
            command.EmployeeId,
            command.PunchedAtUtc,
            command.PunchType,
            command.RecordedByUserId,
            command.RecordedByName,
            null,
            command.Note,
            employees,
            attendanceLogs,
            clock,
            bus,
            ct);
}
