namespace Erp.UseCases.Leave.DecideLeaveRequest;

public sealed record ApproveLeaveRequestCommand(Guid LeaveRequestId, Guid DecidedByUserId, string DecidedByName);

public sealed record DenyLeaveRequestCommand(Guid LeaveRequestId, Guid DecidedByUserId, string DecidedByName, string? Note);

public sealed record CancelLeaveRequestCommand(Guid LeaveRequestId, Guid DecidedByUserId, string DecidedByName, string? Note);
