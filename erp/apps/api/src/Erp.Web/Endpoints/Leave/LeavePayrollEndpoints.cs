using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.Payroll;
using FastEndpoints;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

/// <summary>One request, shaped exactly as the list shows it to this caller.</summary>
public sealed class GetLeaveRequestEndpoint(IMessageBus bus)
    : BusEndpoint<GetLeaveRequestRequest, LeaveRequestResponse>(bus)
{
    public override void Configure()
    {
        Get("/{id:guid}");
        Group<LeaveGroup>();
    }

    protected override object Build(GetLeaveRequestRequest r, Caller caller) => new GetLeaveRequestQuery(r.Id, caller);

    public override async Task HandleAsync(GetLeaveRequestRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await Resolve<IMessageBus>().InvokeAsync<Result<LeaveRequestResult>>(Build(req, caller), ct);
        await SendResultAsync(result switch
        {
            Result<LeaveRequestResult>.Success s => new Result<LeaveRequestResponse>.Success(LeaveRequestResponse.From(s.Value)),
            Result<LeaveRequestResult>.Error e => new Result<LeaveRequestResponse>.Error(e.Code, e.Message),
            _ => new Result<LeaveRequestResponse>.NotFound("Leave request was not found."),
        }, ct);
    }
}

/// <summary>Owner-only: what a correction after close would do, before saving it.</summary>
public sealed class PreviewLeaveCorrectionEndpoint(IMessageBus bus)
    : BusEndpoint<PreviewLeaveCorrectionRequest, LeaveCorrectionPreviewResult>(bus)
{
    public override void Configure()
    {
        Post("/{id:guid}/correction-preview");
        Group<LeaveGroup>();
    }

    protected override object Build(PreviewLeaveCorrectionRequest r, Caller caller)
    {
        HalfDayPeriod? period = null;
        if (!string.IsNullOrWhiteSpace(r.HalfDayPeriod))
        {
            if (!Enum.TryParse<HalfDayPeriod>(r.HalfDayPeriod, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                throw new DomainException("leave.half_day_period", "Half-day period must be Morning or Afternoon.");
            }

            period = parsed;
        }

        return new PreviewLeaveCorrectionQuery(
            r.Id, r.Cancel, r.StartDate, r.EndDate, r.HalfDay, period, r.StartHour, r.EndHour, caller);
    }
}

/// <summary>For the Leave page banner: pending requests the caller could decide that block a payroll close.</summary>
public sealed class GetLeaveCloseBlockersEndpoint(IMessageBus bus)
    : BusEndpoint<EmptyRequest, IReadOnlyList<LeaveCloseBlockerResult>>(bus)
{
    public override void Configure()
    {
        Get("/close-blockers");
        Group<LeaveGroup>();
    }

    protected override object Build(EmptyRequest r, Caller caller) => new GetLeaveCloseBlockersQuery(caller);
}
