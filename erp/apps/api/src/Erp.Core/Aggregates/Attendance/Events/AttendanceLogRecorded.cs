using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance.Events;

/// <summary>
/// Emitted when an attendance punch is captured.
///
/// <para>
/// <b>Timestamps:</b> The envelope's <c>RaisedAt</c> is when this event was
/// emitted by the domain (system clock). <see cref="PunchedAtUtc"/> is the
/// business fact — when the employee actually punched. These intentionally
/// differ for backfilled / manual entries; consumers ordering by punch time
/// must use <see cref="PunchedAtUtc"/>, not the envelope timestamp.
/// </para>
/// </summary>
public sealed record AttendanceLogRecorded(
    Guid LogId,
    Guid EmployeeId,
    Instant PunchedAtUtc,
    AttendanceSource Source,
    PunchType PunchType,
    string? DeviceId,
    Guid? RecordedByUserId,
    string? Note)
    : DomainEvent(LogId, nameof(AttendanceLog), "attendance.recorded");
