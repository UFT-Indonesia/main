using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
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
        IReadRepository<Employee> employees,
        IClock clock,
        CancellationToken ct) =>
        DecideLeaveRequestService.DecideAsync(
            command.LeaveRequestId,
            command.Caller,
            DecisionKind.Approval,
            (request, now) => request.Approve(command.Caller.UserId, command.Caller.Name, now),
            leaveRequests,
            employees,
            clock,
            ct);
}

public static class DenyLeaveRequestHandler
{
    public static Task<Result<LeaveRequestResult>> Handle(
        DenyLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        IClock clock,
        CancellationToken ct) =>
        DecideLeaveRequestService.DecideAsync(
            command.LeaveRequestId,
            command.Caller,
            DecisionKind.Approval,
            (request, now) => request.Deny(command.Caller.UserId, command.Caller.Name, now, command.Note),
            leaveRequests,
            employees,
            clock,
            ct);
}

public static class CancelLeaveRequestHandler
{
    public static Task<Result<LeaveRequestResult>> Handle(
        CancelLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        IClock clock,
        CancellationToken ct) =>
        DecideLeaveRequestService.DecideAsync(
            command.LeaveRequestId,
            command.Caller,
            DecisionKind.Cancellation,
            (request, now) => request.Cancel(command.Caller.UserId, command.Caller.Name, now, command.Note),
            leaveRequests,
            employees,
            clock,
            ct);
}

internal enum DecisionKind
{
    /// <summary>Approve or deny — requires authority over the subject, and never the requester.</summary>
    Approval,

    /// <summary>Cancel — the subject may always cancel their own, requester or not.</summary>
    Cancellation,
}

internal static class DecideLeaveRequestService
{
    internal static async Task<Result<LeaveRequestResult>> DecideAsync(
        Guid leaveRequestId,
        Caller caller,
        DecisionKind kind,
        Action<LeaveRequest, Instant> decide,
        IRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        IClock clock,
        CancellationToken ct)
    {
        var request = await leaveRequests.FirstOrDefaultAsync(
            new LeaveRequestByIdSpec(new LeaveRequestId(leaveRequestId)), ct);
        if (request is null)
        {
            return new Result<LeaveRequestResult>.NotFound("Leave request was not found.");
        }

        // Loaded explicitly rather than off the navigation: authority hinges on the subject's
        // current role and reporting line, so it should not depend on an Include staying put.
        var subject = await employees.GetByIdAsync(request.EmployeeId, ct);
        if (subject is null)
        {
            return new Result<LeaveRequestResult>.NotFound("The employee this request belongs to was not found.");
        }

        var permitted = kind == DecisionKind.Cancellation
            ? LeaveRules.CanCancel(caller, subject)
            : LeaveRules.CanDecideFor(caller, subject)
              && !LeaveRules.IsRequester(caller, request.RequestedByUserId);

        if (!permitted)
        {
            return new Result<LeaveRequestResult>.Error(
                ResultErrors.Forbidden, "You cannot decide this leave request.");
        }

        decide(request, clock.GetCurrentInstant());
        await leaveRequests.UpdateAsync(request, ct);

        var (canDecide, canCancel) = LeaveRequestResult.PermissionsFor(caller, request, subject);
        return new Result<LeaveRequestResult>.Success(
            LeaveRequestResult.From(
                request,
                // Single-request responses do not run the yearly rollup query.
                approvedWorkdaysThisYear: null,
                employeeFullName: subject.FullName,
                canDecide: canDecide,
                canCancel: canCancel,
                // Only reachable once authority to decide or cancel has been established.
                canReadDetails: true));
    }
}
