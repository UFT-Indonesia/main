using Erp.UseCases.Common;

namespace Erp.UseCases.Attendance.DeleteAttendanceLogNote;

public sealed record DeleteAttendanceLogNoteCommand(Guid LogId, Guid NoteId, Caller Caller);
