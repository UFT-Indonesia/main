using Erp.UseCases.Common;

namespace Erp.UseCases.Leave.DecideLeaveRequest;

public sealed record ApproveLeaveRequestCommand(Guid LeaveRequestId, Caller Caller);

public sealed record DenyLeaveRequestCommand(Guid LeaveRequestId, Caller Caller, string? Note);

public sealed record CancelLeaveRequestCommand(Guid LeaveRequestId, Caller Caller, string? Note);
