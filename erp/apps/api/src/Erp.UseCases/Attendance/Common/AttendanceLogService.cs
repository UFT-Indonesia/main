using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Attendance.Common;

internal static class AttendanceLogService
{
    internal static async Task<Result<AttendanceResult>> RecordAsync(
        Guid employeeId,
        DateTimeOffset punchedAtUtc,
        string punchTypeValue,
        Guid? recordedByUserId,
        string? deviceId,
        string? note,
        IReadRepository<Employee> employees,
        IRepository<AttendanceLog> attendanceLogs,
        CancellationToken ct)
    {
        if (!TryParsePunchType(punchTypeValue, out var punchType))
        {
            return new Result<AttendanceResult>.Error("attendance.punch_type", "Punch type must be In or Out.");
        }

        var typedEmployeeId = new EmployeeId(employeeId);
        var employee = await employees.GetByIdAsync(typedEmployeeId, ct);
        if (employee is null)
        {
            return new Result<AttendanceResult>.NotFound("Employee was not found.");
        }

        AttendanceLog log;
        try
        {
            log = recordedByUserId.HasValue
                ? AttendanceLog.Manual(typedEmployeeId, Instant.FromDateTimeOffset(punchedAtUtc), punchType, recordedByUserId.Value, note)
                : AttendanceLog.FromDevice(typedEmployeeId, Instant.FromDateTimeOffset(punchedAtUtc), punchType, deviceId ?? string.Empty);
        }
        catch (DomainException ex)
        {
            return new Result<AttendanceResult>.Error(ex.Code ?? "attendance.validation", ex.Message);
        }

        await attendanceLogs.AddAsync(log, ct);

        return new Result<AttendanceResult>.Success(ToResult(log));
    }

    private static bool TryParsePunchType(string value, out PunchType punchType)
    {
        punchType = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (int.TryParse(value, out _))
        {
            return false;
        }

        return Enum.TryParse(value, ignoreCase: true, out punchType);
    }

    private static AttendanceResult ToResult(AttendanceLog log) => new()
    {
        Id = log.Id.Value,
        EmployeeId = log.EmployeeId.Value,
        PunchedAtUtc = log.PunchedAtUtc.ToDateTimeOffset(),
        Source = log.Source.ToString(),
        PunchType = log.PunchType.ToString(),
        DeviceId = log.DeviceId,
        RecordedByUserId = log.RecordedByUserId,
        Note = log.Note,
    };
}
