using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance;

/// <summary>
/// One authored, append-only note on a punch. Created and removed only through
/// <see cref="AttendanceLog.AddNote"/> / <see cref="AttendanceLog.RemoveNote"/> —
/// notes are never edited in place; a mistake is deleted and rewritten.
/// <para>
/// <see cref="CreatedByName"/> is a display-name snapshot taken at write time so the
/// audit trail stays readable even if the authoring user is later renamed or removed.
/// </para>
/// </summary>
public sealed class AttendanceLogNote : Entity
{
    // EF Core constructor.
    private AttendanceLogNote() { }

    internal AttendanceLogNote(
        AttendanceLogId attendanceLogId,
        string text,
        Guid createdByUserId,
        string createdByName,
        Instant createdAtUtc)
    {
        AttendanceLogId = attendanceLogId;
        Text = text;
        CreatedByUserId = createdByUserId;
        CreatedByName = createdByName;
        CreatedAtUtc = createdAtUtc;
    }

    public AttendanceLogId AttendanceLogId { get; private set; }

    public string Text { get; private set; } = default!;

    public Guid CreatedByUserId { get; private set; }

    public string CreatedByName { get; private set; } = default!;

    public Instant CreatedAtUtc { get; private set; }
}
