namespace Erp.UseCases.Attendance.DeleteAttendanceLogNote;

public sealed record DeleteAttendanceLogNoteCommand(Guid LogId, Guid NoteId);
