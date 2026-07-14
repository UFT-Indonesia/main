using Erp.Core.Aggregates.Attendance;

namespace Erp.UseCases.Attendance.Common;

public sealed class AttendanceLogNoteResult
{
    public Guid Id { get; init; }
    public string Text { get; init; } = default!;
    public Guid CreatedByUserId { get; init; }
    public string CreatedByName { get; init; } = default!;
    public DateTimeOffset CreatedAtUtc { get; init; }

    public static AttendanceLogNoteResult From(AttendanceLogNote note) => new()
    {
        Id = note.Id,
        Text = note.Text,
        CreatedByUserId = note.CreatedByUserId,
        CreatedByName = note.CreatedByName,
        CreatedAtUtc = note.CreatedAtUtc.ToDateTimeOffset(),
    };

    public static IReadOnlyList<AttendanceLogNoteResult> FromLog(AttendanceLog log) =>
        log.Notes.OrderBy(note => note.CreatedAtUtc).Select(From).ToList();
}
