using Erp.UseCases.Common;

namespace Erp.UseCases.Leave.CreateLeaveRequest;

public sealed record CreateLeaveRequestCommand(
    Guid EmployeeId,
    string Type,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    Caller Caller);
