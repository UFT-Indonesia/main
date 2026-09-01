using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.GetBlockedLeaveDates;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

/// <summary>
/// Approved leave ranges for one employee inside a window, for the date pickers to grey out.
/// Open to any signed-in user, like the leave list it is derived from — it exposes dates only,
/// never the type or the reason.
/// </summary>
[Authorize]
public sealed class GetBlockedLeaveDatesEndpoint
    : Endpoint<GetBlockedLeaveDatesRequest, BlockedLeaveDatesResponse>
{
    private readonly IMessageBus _bus;

    public GetBlockedLeaveDatesEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/blocked-dates");
        Group<LeaveGroup>();
    }

    public override async Task HandleAsync(GetBlockedLeaveDatesRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<BlockedLeaveDatesResult>>(
            new GetBlockedLeaveDatesQuery(req.EmployeeId, req.From, req.To, caller), ct);

        if (result is Result<BlockedLeaveDatesResult>.Success s)
        {
            await SendOkAsync(new BlockedLeaveDatesResponse
            {
                Ranges = [.. s.Value.Ranges.Select(r => new BlockedLeaveRangeResponse
                {
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                })],
            }, ct);
            return;
        }

        if (result is Result<BlockedLeaveDatesResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
