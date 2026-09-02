using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.EditLeaveRequest;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

/// <summary>
/// Moves an existing request's dates. Authority is <see cref="LeaveRules.CanDecideFor"/> — the
/// same standing approving it takes — so the role gate here is only "must be signed in".
/// </summary>
[Authorize]
public sealed class EditLeaveRequestEndpoint : Endpoint<EditLeaveRequestRequest, LeaveRequestResponse>
{
    private readonly IMessageBus _bus;

    public EditLeaveRequestEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Post("/{id:guid}/edit");
        Group<LeaveGroup>();
    }

    public override async Task HandleAsync(EditLeaveRequestRequest req, CancellationToken ct)
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

        var result = await _bus.InvokeAsync<Result<LeaveRequestResult>>(new EditLeaveRequestCommand(
            req.Id,
            req.StartDate,
            req.EndDate,
            req.HalfDay,
            halfDayPeriod,
            req.StartHour,
            req.EndHour,
            caller), ct);

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
