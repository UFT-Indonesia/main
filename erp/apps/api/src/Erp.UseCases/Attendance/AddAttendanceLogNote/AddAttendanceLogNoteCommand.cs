using Erp.UseCases.Common;

namespace Erp.UseCases.Attendance.AddAttendanceLogNote;

public sealed record AddAttendanceLogNoteCommand(
    Guid LogId,
    string Text,
    Caller Caller);
