namespace Erp.UseCases.Attendance.AddAttendanceLogNote;

public sealed record AddAttendanceLogNoteCommand(
    Guid LogId,
    string Text,
    Guid CreatedByUserId,
    string CreatedByName);
