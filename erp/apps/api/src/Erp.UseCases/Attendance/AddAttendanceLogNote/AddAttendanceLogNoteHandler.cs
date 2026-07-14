using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using NodaTime;

namespace Erp.UseCases.Attendance.AddAttendanceLogNote;

public static class AddAttendanceLogNoteHandler
{
    public static async Task<Result<AttendanceLogNoteResult>> Handle(
        AddAttendanceLogNoteCommand command,
        IRepository<AttendanceLog> attendanceLogs,
        IClock clock,
        CancellationToken ct)
    {
        var log = await attendanceLogs.FirstOrDefaultAsync(
            new AttendanceLogByIdWithNotesSpec(new AttendanceLogId(command.LogId)), ct);
        if (log is null)
        {
            return new Result<AttendanceLogNoteResult>.NotFound("Attendance log was not found.");
        }

        // Domain validation errors (empty text, over-length, missing author) throw
        // DomainException and bubble to the global exception handler.
        var note = log.AddNote(
            command.Text, command.CreatedByUserId, command.CreatedByName, clock.GetCurrentInstant());
        await attendanceLogs.UpdateAsync(log, ct);

        return new Result<AttendanceLogNoteResult>.Success(AttendanceLogNoteResult.From(note));
    }
}
