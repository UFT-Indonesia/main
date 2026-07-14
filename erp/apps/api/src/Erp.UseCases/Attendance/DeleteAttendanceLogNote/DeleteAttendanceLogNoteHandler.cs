using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;

namespace Erp.UseCases.Attendance.DeleteAttendanceLogNote;

public static class DeleteAttendanceLogNoteHandler
{
    public static async Task<Result<bool>> Handle(
        DeleteAttendanceLogNoteCommand command,
        IRepository<AttendanceLog> attendanceLogs,
        CancellationToken ct)
    {
        var log = await attendanceLogs.FirstOrDefaultAsync(
            new AttendanceLogByIdWithNotesSpec(new AttendanceLogId(command.LogId)), ct);
        if (log is null)
        {
            return new Result<bool>.NotFound("Attendance log was not found.");
        }

        if (!log.RemoveNote(command.NoteId))
        {
            return new Result<bool>.NotFound("Note was not found on this attendance log.");
        }

        await attendanceLogs.UpdateAsync(log, ct);
        return new Result<bool>.Success(true);
    }
}
