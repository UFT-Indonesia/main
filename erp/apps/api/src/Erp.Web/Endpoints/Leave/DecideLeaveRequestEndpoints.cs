using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.DecideLeaveRequest;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

/// <summary>
/// Shared plumbing for the three decision endpoints (approve/deny/cancel). Authority is
/// enforced per-request by <see cref="LeaveRules"/> against the subject's role and reporting
/// line, so the role gate here is only "must be signed in".
/// </summary>
[Authorize]
public abstract class DecideLeaveRequestEndpointBase : Endpoint<DecideLeaveRequestRequest, LeaveRequestResponse>
{
    private readonly IMessageBus _bus;

    protected DecideLeaveRequestEndpointBase(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Route segment under /api/leave/{id:guid}/ (e.g. "approve").</summary>
    protected abstract string Action { get; }

    protected abstract object BuildCommand(DecideLeaveRequestRequest req, Caller caller);

    public override void Configure()
    {
        Post($"/{{id:guid}}/{Action}");
        Group<LeaveGroup>();
    }

    public override async Task HandleAsync(DecideLeaveRequestRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<LeaveRequestResult>>(BuildCommand(req, caller), ct);

        if (result is Result<LeaveRequestResult>.Success s)
        {
            await SendOkAsync(LeaveRequestResponse.From(s.Value), ct);
            return;
        }

        if (result is Result<LeaveRequestResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<LeaveRequestResult>.Error e)
        {
            if (e.Code == ResultErrors.Forbidden)
            {
                await SendForbiddenAsync(ct);
                return;
            }

            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}

public sealed class ApproveLeaveRequestEndpoint : DecideLeaveRequestEndpointBase
{
    public ApproveLeaveRequestEndpoint(IMessageBus bus) : base(bus) { }

    protected override string Action => "approve";

    protected override object BuildCommand(DecideLeaveRequestRequest req, Caller caller) =>
        new ApproveLeaveRequestCommand(req.Id, caller);
}

public sealed class DenyLeaveRequestEndpoint : DecideLeaveRequestEndpointBase
{
    public DenyLeaveRequestEndpoint(IMessageBus bus) : base(bus) { }

    protected override string Action => "deny";

    protected override object BuildCommand(DecideLeaveRequestRequest req, Caller caller) =>
        new DenyLeaveRequestCommand(req.Id, caller, req.Note);
}

public sealed class CancelLeaveRequestEndpoint : DecideLeaveRequestEndpointBase
{
    public CancelLeaveRequestEndpoint(IMessageBus bus) : base(bus) { }

    protected override string Action => "cancel";

    protected override object BuildCommand(DecideLeaveRequestRequest req, Caller caller) =>
        new CancelLeaveRequestCommand(req.Id, caller, req.Note);
}
