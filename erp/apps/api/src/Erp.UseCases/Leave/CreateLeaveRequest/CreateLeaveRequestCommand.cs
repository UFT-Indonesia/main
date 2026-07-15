namespace Erp.UseCases.Leave.CreateLeaveRequest;

public sealed record CreateLeaveRequestCommand(
    Guid EmployeeId,
    string Type,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason,
    Guid RequestedByUserId);
