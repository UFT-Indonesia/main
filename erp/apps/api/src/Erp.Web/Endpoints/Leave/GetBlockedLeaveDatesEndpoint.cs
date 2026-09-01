using Erp.Core.Aggregates.Leave;
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

        HalfDayPeriod? halfDayPeriod = null;
        if (!string.IsNullOrWhiteSpace(req.HalfDayPeriod))
        {
            if (!Enum.TryParse<HalfDayPeriod>(req.HalfDayPeriod, ignoreCase: true, out var parsedPeriod)
                || !Enum.IsDefined(parsedPeriod))
            {
                throw new DomainException(
                    "leave.half_day_period", "Half-day period must be Morning or Afternoon.");
            }

            halfDayPeriod = parsedPeriod;
        }

        var result = await _bus.InvokeAsync<Result<BlockedLeaveDatesResult>>(
            new GetBlockedLeaveDatesQuery(
                req.EmployeeId, req.From, req.To,
                req.HalfDay, halfDayPeriod, req.StartHour, req.EndHour, caller),
            ct);

        if (result is Result<BlockedLeaveDatesResult>.Success s)
        {
            await SendOkAsync(new BlockedLeaveDatesResponse
            {
                BlockedDates = s.Value.BlockedDates,
                PartialDates = s.Value.PartialDates,
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
