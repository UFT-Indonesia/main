using Erp.Core.Aggregates.Attendance.Events;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance;

public sealed class AttendanceLog : AggregateRoot<AttendanceLogId>
{
    // EF Core constructor.
    private AttendanceLog() { }

    private AttendanceLog(
        AttendanceLogId id,
        EmployeeId employeeId,
        Instant punchedAtUtc,
        AttendanceSource source,
        PunchType punchType,
        string? deviceId,
        string? note,
        Guid? recordedByUserId)
        : base(id)
    {
        EmployeeId = employeeId;
        PunchedAtUtc = punchedAtUtc;
        Source = source;
        PunchType = punchType;
        DeviceId = deviceId;
        Note = note;
        RecordedByUserId = recordedByUserId;
    }

    public EmployeeId EmployeeId { get; private set; }

    /// <summary>UTC instant of the actual punch (business fact, may be backfilled).</summary>
    public Instant PunchedAtUtc { get; private set; }

    public AttendanceSource Source { get; private set; }

    public PunchType PunchType { get; private set; }

    public string? DeviceId { get; private set; }

    public string? Note { get; private set; }

    public Guid? RecordedByUserId { get; private set; }

    public static AttendanceLog FromDevice(
        EmployeeId employeeId,
        Instant punchedAtUtc,
        PunchType punchType,
        string deviceId)
    {
        if (employeeId == EmployeeId.Empty)
        {
            throw new DomainException("attendance.employee_id", "Employee id is required.");
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new DomainException("attendance.device_id", "Device id is required.");
        }

        var log = new AttendanceLog(
            AttendanceLogId.New(),
            employeeId,
            punchedAtUtc,
            AttendanceSource.Device,
            punchType,
            deviceId.Trim(),
            null,
            null);

        log.RaiseDomainEvent(new AttendanceLogRecorded(
            log.Id.Value,
            log.EmployeeId.Value,
            log.PunchedAtUtc,
            log.Source,
            log.PunchType,
            log.DeviceId,
            log.RecordedByUserId,
            log.Note));

        return log;
    }

    public static AttendanceLog Manual(
        EmployeeId employeeId,
        Instant punchedAtUtc,
        PunchType punchType,
        Guid recordedByUserId,
        string? note = null)
    {
        if (employeeId == EmployeeId.Empty)
        {
            throw new DomainException("attendance.employee_id", "Employee id is required.");
        }

        if (recordedByUserId == Guid.Empty)
        {
            throw new DomainException(
                "attendance.recorded_by_required",
                "Manual entries require an authenticated recorder.");
        }

        var log = new AttendanceLog(
            AttendanceLogId.New(),
            employeeId,
            punchedAtUtc,
            AttendanceSource.Manual,
            punchType,
            null,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            recordedByUserId);

        log.RaiseDomainEvent(new AttendanceLogRecorded(
            log.Id.Value,
            log.EmployeeId.Value,
            log.PunchedAtUtc,
            log.Source,
            log.PunchType,
            log.DeviceId,
            log.RecordedByUserId,
            log.Note));

        return log;
    }
}
