using System.Security.Claims;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.DecideLeaveRequest;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

/// <summary>Shared plumbing for the three decision endpoints (approve/deny/cancel).</summary>
public abstract class DecideLeaveRequestEndpointBase : Endpoint<DecideLeaveRequestRequest, LeaveRequestResponse>
{
    private readonly IMessageBus _bus;

    protected DecideLeaveRequestEndpointBase(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Route segment under /api/leave/{id:guid}/ (e.g. "approve").</summary>
    protected abstract string Action { get; }

    protected abstract object BuildCommand(DecideLeaveRequestRequest req, Guid userId, string userName);

    public override void Configure()
    {
        Post($"/{{id:guid}}/{Action}");
        Group<LeaveGroup>();
    }

    public override async Task HandleAsync(DecideLeaveRequestRequest req, CancellationToken ct)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var userName = User.FindFirstValue(ClaimTypes.Name) ?? "—";

        var result = await _bus.InvokeAsync<Result<LeaveRequestResult>>(
            BuildCommand(req, userId, userName), ct);

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
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}

[Authorize(Roles = "Owner,Manager")]
public sealed class ApproveLeaveRequestEndpoint : DecideLeaveRequestEndpointBase
{
    public ApproveLeaveRequestEndpoint(IMessageBus bus) : base(bus) { }

    protected override string Action => "approve";

    protected override object BuildCommand(DecideLeaveRequestRequest req, Guid userId, string userName) =>
        new ApproveLeaveRequestCommand(req.Id, userId, userName);
}

[Authorize(Roles = "Owner,Manager")]
public sealed class DenyLeaveRequestEndpoint : DecideLeaveRequestEndpointBase
{
    public DenyLeaveRequestEndpoint(IMessageBus bus) : base(bus) { }

    protected override string Action => "deny";

    protected override object BuildCommand(DecideLeaveRequestRequest req, Guid userId, string userName) =>
        new DenyLeaveRequestCommand(req.Id, userId, userName, req.Note);
}

[Authorize(Roles = "Owner,Manager")]
public sealed class CancelLeaveRequestEndpoint : DecideLeaveRequestEndpointBase
{
    public CancelLeaveRequestEndpoint(IMessageBus bus) : base(bus) { }

    protected override string Action => "cancel";

    protected override object BuildCommand(DecideLeaveRequestRequest req, Guid userId, string userName) =>
        new CancelLeaveRequestCommand(req.Id, userId, userName, req.Note);
}
