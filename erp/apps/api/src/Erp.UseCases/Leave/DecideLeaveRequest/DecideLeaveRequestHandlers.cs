using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Leave.Common;
using NodaTime;

namespace Erp.UseCases.Leave.DecideLeaveRequest;

// Domain-level lifecycle violations (already decided, not cancellable) throw
// DomainException and bubble to the global exception handler as 400s.

public static class ApproveLeaveRequestHandler
{
    public static Task<Result<LeaveRequestResult>> Handle(
        ApproveLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IClock clock,
        CancellationToken ct) =>
        DecideLeaveRequestService.DecideAsync(
            command.LeaveRequestId,
            (request, now) => request.Approve(command.DecidedByUserId, command.DecidedByName, now),
            leaveRequests,
            clock,
            ct);
}

public static class DenyLeaveRequestHandler
{
    public static Task<Result<LeaveRequestResult>> Handle(
        DenyLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IClock clock,
        CancellationToken ct) =>
        DecideLeaveRequestService.DecideAsync(
            command.LeaveRequestId,
            (request, now) => request.Deny(command.DecidedByUserId, command.DecidedByName, now, command.Note),
            leaveRequests,
            clock,
            ct);
}

public static class CancelLeaveRequestHandler
{
    public static Task<Result<LeaveRequestResult>> Handle(
        CancelLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IClock clock,
        CancellationToken ct) =>
        DecideLeaveRequestService.DecideAsync(
            command.LeaveRequestId,
            (request, now) => request.Cancel(command.DecidedByUserId, command.DecidedByName, now, command.Note),
            leaveRequests,
            clock,
            ct);
}

internal static class DecideLeaveRequestService
{
    internal static async Task<Result<LeaveRequestResult>> DecideAsync(
        Guid leaveRequestId,
        Action<LeaveRequest, Instant> decide,
        IRepository<LeaveRequest> leaveRequests,
        IClock clock,
        CancellationToken ct)
    {
        var request = await leaveRequests.FirstOrDefaultAsync(
            new LeaveRequestByIdSpec(new LeaveRequestId(leaveRequestId)), ct);
        if (request is null)
        {
            return new Result<LeaveRequestResult>.NotFound("Leave request was not found.");
        }

        decide(request, clock.GetCurrentInstant());
        await leaveRequests.UpdateAsync(request, ct);

        return new Result<LeaveRequestResult>.Success(LeaveRequestResult.From(request));
    }
}
