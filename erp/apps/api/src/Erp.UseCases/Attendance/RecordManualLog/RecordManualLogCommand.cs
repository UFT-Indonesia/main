using Erp.UseCases.Common;

namespace Erp.UseCases.Attendance.RecordManualLog;

public sealed record RecordManualLogCommand(
    Guid EmployeeId,
    DateTimeOffset PunchedAtUtc,
    string PunchType,
    string? Note,
    Caller Caller);
