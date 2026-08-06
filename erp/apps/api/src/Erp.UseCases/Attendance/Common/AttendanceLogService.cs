using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Attendance.Common;

internal static class AttendanceLogService
{
    internal static async Task<Result<AttendanceResult>> RecordAsync(
        Guid employeeId,
        DateTimeOffset punchedAtUtc,
        string punchTypeValue,
        Guid? recordedByUserId,
        string? recordedByName,
        string? deviceId,
        string? note,
        Caller? caller,
        IReadRepository<Employee> employees,
        IRepository<AttendanceLog> attendanceLogs,
        IClock clock,
        IMessageBus bus,
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

        // New punches stop at termination; corrections to existing ones stay open so payroll
        // can still close the final period out.
        if (employee.Status == EmployeeStatus.Terminated)
        {
            return new Result<AttendanceResult>.Error(
                "attendance.employee_terminated", "Cannot record attendance for a terminated employee.");
        }

        // A manual punch asserts someone was physically present, so it stays inside the
        // caller's reporting line. Device punches have no caller — the signature is the gate.
        if (caller is { } writer && !AttendanceRules.CanWriteFor(writer, employee))
        {
            return new Result<AttendanceResult>.Error(
                ResultErrors.Forbidden, "You cannot record attendance for this employee.");
        }

        AttendanceLog log;
        try
        {
            log = recordedByUserId.HasValue
                ? AttendanceLog.Manual(typedEmployeeId, Instant.FromDateTimeOffset(punchedAtUtc), punchType, recordedByUserId.Value)
                : AttendanceLog.FromDevice(typedEmployeeId, Instant.FromDateTimeOffset(punchedAtUtc), punchType, deviceId ?? string.Empty);

            // The manual-entry dialog's optional "catatan" becomes the first note in the
            // punch's thread, authored by the recorder at the actual time of writing.
            if (recordedByUserId.HasValue && !string.IsNullOrWhiteSpace(note))
            {
                log.AddNote(note, recordedByUserId.Value, recordedByName ?? "—", clock.GetCurrentInstant());
            }
        }
        catch (DomainException ex)
        {
            return new Result<AttendanceResult>.Error(ex.Code ?? "attendance.validation", ex.Message);
        }

        await attendanceLogs.AddAsync(log, ct);
        foreach (var domainEvent in log.DomainEvents.OfType<AttendanceLogRecorded>())
        {
            await bus.PublishAsync(domainEvent);
        }

        return new Result<AttendanceResult>.Success(ToResult(log));
    }

    /// <summary>
    /// Write authority for an existing punch, which hangs off the employee it belongs to.
    /// Returns null when permitted; otherwise the error to hand back.
    /// </summary>
    internal static async Task<Result<T>.Error?> CheckWriteAccessAsync<T>(
        Caller caller,
        EmployeeId employeeId,
        IReadRepository<Employee> employees,
        CancellationToken ct)
    {
        var employee = await employees.GetByIdAsync(employeeId, ct);
        if (employee is null)
        {
            return new Result<T>.Error("attendance.employee_missing", "The employee this punch belongs to was not found.");
        }

        return AttendanceRules.CanWriteFor(caller, employee)
            ? null
            : new Result<T>.Error(ResultErrors.Forbidden, "You cannot change attendance for this employee.");
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

    internal static AttendanceResult ToResult(AttendanceLog log) => new()
    {
        Id = log.Id.Value,
        EmployeeId = log.EmployeeId.Value,
        PunchedAtUtc = log.PunchedAtUtc.ToDateTimeOffset(),
        Source = log.Source.ToString(),
        PunchType = log.PunchType.ToString(),
        DeviceId = log.DeviceId,
        RecordedByUserId = log.RecordedByUserId,
        Notes = AttendanceLogNoteResult.FromLog(log),
    };
}
