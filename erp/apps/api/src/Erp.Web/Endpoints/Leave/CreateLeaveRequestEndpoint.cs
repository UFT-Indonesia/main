using System.Security.Claims;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.CreateLeaveRequest;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

[Authorize(Roles = "Owner,Manager")]
public sealed class CreateLeaveRequestEndpoint : Endpoint<CreateLeaveRequestRequest, LeaveRequestResponse>
{
    private readonly IMessageBus _bus;

    public CreateLeaveRequestEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Post("/");
        Group<LeaveGroup>();
    }

    public override async Task HandleAsync(CreateLeaveRequestRequest req, CancellationToken ct)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<LeaveRequestResult>>(new CreateLeaveRequestCommand(
            req.EmployeeId,
            req.Type,
            req.StartDate,
            req.EndDate,
            req.Reason,
            userId), ct);

        if (result is Result<LeaveRequestResult>.Success s)
        {
            await SendCreatedAtAsync<CreateLeaveRequestEndpoint>(
                null, LeaveRequestResponse.From(s.Value), cancellation: ct);
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
