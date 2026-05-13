using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Attendance;

public sealed class AttendanceLogHandler
{
    public static Task<AttendanceResult> Handle(
        RecordManualAttendanceLog command,
        IReadRepository<Employee> employees,
        IRepository<AttendanceLog> attendanceLogs,
        CancellationToken ct) =>
        RecordAsync(
            command.EmployeeId,
            command.PunchedAtUtc,
            command.PunchType,
            command.RecordedByUserId,
            null,
            command.Note,
            employees,
            attendanceLogs,
            ct);

    public static Task<AttendanceResult> Handle(
        RecordDeviceAttendanceLog command,
        IReadRepository<Employee> employees,
        IRepository<AttendanceLog> attendanceLogs,
        CancellationToken ct) =>
        RecordAsync(
            command.EmployeeId,
            command.PunchedAtUtc,
            command.PunchType,
            null,
            command.DeviceId,
            null,
            employees,
            attendanceLogs,
            ct);

    private static async Task<AttendanceResult> RecordAsync(
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
        var typedEmployeeId = new EmployeeId(employeeId);
        var employee = await employees.GetByIdAsync(typedEmployeeId, ct);
        if (employee is null)
        {
            throw new KeyNotFoundException("Employee was not found.");
        }

        if (!TryParsePunchType(punchTypeValue, out var punchType))
        {
            throw new DomainException("attendance.punch_type", "Punch type must be In or Out.");
        }

        var log = recordedByUserId.HasValue
            ? AttendanceLog.Manual(typedEmployeeId, Instant.FromDateTimeOffset(punchedAtUtc), punchType, recordedByUserId.Value, note)
            : AttendanceLog.FromDevice(typedEmployeeId, Instant.FromDateTimeOffset(punchedAtUtc), punchType, deviceId ?? string.Empty);

        await attendanceLogs.AddAsync(log, ct);
        await attendanceLogs.SaveChangesAsync(ct);

        return ToResult(log);
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
