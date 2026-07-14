using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance;

public sealed class AttendanceLog : AggregateRoot<AttendanceLogId>
{
    public const int NoteMaxLength = 500;

    private readonly List<AttendanceLogNote> _notes = [];

    // EF Core constructor.
    private AttendanceLog() { }

    private AttendanceLog(
        AttendanceLogId id,
        EmployeeId employeeId,
        Instant punchedAtUtc,
        AttendanceSource source,
        PunchType punchType,
        string? deviceId,
        Guid? recordedByUserId)
        : base(id)
    {
        EmployeeId = employeeId;
        PunchedAtUtc = punchedAtUtc;
        Source = source;
        PunchType = punchType;
        DeviceId = deviceId;
        RecordedByUserId = recordedByUserId;
    }

    public EmployeeId EmployeeId { get; private set; }

    // EF Core navigation — read-only, not part of domain behavior.
    public Employee? Employee { get; private set; }

    /// <summary>UTC instant of the actual punch (business fact, may be backfilled).</summary>
    public Instant PunchedAtUtc { get; private set; }

    public AttendanceSource Source { get; private set; }

    public PunchType PunchType { get; private set; }

    public string? DeviceId { get; private set; }

    public Guid? RecordedByUserId { get; private set; }

    /// <summary>Authored notes on this punch, append-only, oldest first.</summary>
    public IReadOnlyCollection<AttendanceLogNote> Notes => _notes.AsReadOnly();

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
            null);

        log.RaiseDomainEvent(new AttendanceLogRecorded(
            log.Id.Value,
            log.EmployeeId.Value,
            log.PunchedAtUtc,
            log.Source,
            log.PunchType,
            log.DeviceId,
            log.RecordedByUserId));

        return log;
    }

    public static AttendanceLog Manual(
        EmployeeId employeeId,
        Instant punchedAtUtc,
        PunchType punchType,
        Guid recordedByUserId)
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
            recordedByUserId);

        log.RaiseDomainEvent(new AttendanceLogRecorded(
            log.Id.Value,
            log.EmployeeId.Value,
            log.PunchedAtUtc,
            log.Source,
            log.PunchType,
            log.DeviceId,
            log.RecordedByUserId));

        return log;
    }

    /// <summary>
    /// Corrects a punch after the fact (Owner/Manager action). Callers are
    /// responsible for recomputing the affected <see cref="AttendanceDay"/> rows.
    /// Notes are managed separately via <see cref="AddNote"/> / <see cref="RemoveNote"/>.
    /// </summary>
    public void UpdateManualEntry(Instant punchedAtUtc, PunchType punchType)
    {
        if (PunchedAtUtc == punchedAtUtc && PunchType == punchType)
        {
            return;
        }

        PunchedAtUtc = punchedAtUtc;
        PunchType = punchType;
    }

    /// <summary>Appends an authored note. Notes are immutable once written.</summary>
    public AttendanceLogNote AddNote(string text, Guid createdByUserId, string createdByName, Instant nowUtc)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("attendance_note.text", "Note text is required.");
        }

        var trimmed = text.Trim();
        if (trimmed.Length > NoteMaxLength)
        {
            throw new DomainException(
                "attendance_note.text_length", $"Note text cannot exceed {NoteMaxLength} characters.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new DomainException("attendance_note.author", "Notes require an authenticated author.");
        }

        if (string.IsNullOrWhiteSpace(createdByName))
        {
            throw new DomainException("attendance_note.author_name", "Notes require the author's display name.");
        }

        var note = new AttendanceLogNote(Id, trimmed, createdByUserId, createdByName.Trim(), nowUtc);
        _notes.Add(note);
        return note;
    }

    /// <summary>Removes a note by id. Returns false when the note does not belong to this punch.</summary>
    public bool RemoveNote(Guid noteId)
    {
        var note = _notes.FirstOrDefault(n => n.Id == noteId);
        if (note is null)
        {
            return false;
        }

        _notes.Remove(note);
        return true;
    }
}
