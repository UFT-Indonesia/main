using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.GetLeaveAttachment;
using Erp.UseCases.Overtime.Common;
using Erp.UseCases.Overtime.GajiPremi;
using Erp.UseCases.Overtime.Rapels;
using Erp.Web.Endpoints.Overtime;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Payroll;

public sealed class GajiPremiPeriodRequest
{
    public DateOnly? PeriodStart { get; init; }
}

public sealed class CloseGajiPremiRequest
{
    public DateOnly PeriodStart { get; init; }
}

public sealed class ApproveRapelRequest
{
    public Guid Id { get; init; }

    /// <summary>Null keeps the suggested amount.</summary>
    public decimal? Amount { get; init; }
}

public sealed class RejectRapelRequest
{
    public Guid Id { get; init; }
    public string? Note { get; init; }
}

/// <summary>
/// Multipart (WA proof required). A claim on one overtime with the times worked. An employee leaves
/// <see cref="Amount"/> empty; an Owner adding one directly may set it, or leave it to keep the suggestion.
/// </summary>
public sealed class CreateRapelRequest
{
    public Guid AssignmentId { get; init; }
    public TimeOnly From { get; init; }
    public TimeOnly To { get; init; }
    public string Note { get; init; } = default!;
    public decimal? Amount { get; init; }
    public IFormFile? Attachment { get; init; }
}

public sealed class GetGajiPremiPeriodEndpoint(IMessageBus bus) : BusEndpoint<GajiPremiPeriodRequest, GajiPremiPeriodResult>(bus)
{
    public override void Configure()
    {
        Get("/gaji-premi");
        Group<PayrollGroup>();
    }

    protected override object Build(GajiPremiPeriodRequest r, Caller caller) => new GetGajiPremiPeriodQuery(r.PeriodStart, caller);
}

public sealed class CloseGajiPremiPeriodEndpoint(IMessageBus bus) : BusEndpoint<CloseGajiPremiRequest, GajiPremiPeriodResult>(bus)
{
    public override void Configure()
    {
        Post("/gaji-premi/close");
        Group<PayrollGroup>();
    }

    protected override object Build(CloseGajiPremiRequest r, Caller caller) => new CloseGajiPremiPeriodCommand(r.PeriodStart, caller);
}

public sealed class ApproveRapelEndpoint(IMessageBus bus) : BusEndpoint<ApproveRapelRequest, RapelResult>(bus)
{
    public override void Configure()
    {
        Post("/rapel/{id:guid}/approve");
        Group<PayrollGroup>();
    }

    protected override object Build(ApproveRapelRequest r, Caller caller) => new ApproveRapelCommand(r.Id, r.Amount, caller);
}

public sealed class RejectRapelEndpoint(IMessageBus bus) : BusEndpoint<RejectRapelRequest, RapelResult>(bus)
{
    public override void Configure()
    {
        Post("/rapel/{id:guid}/reject");
        Group<PayrollGroup>();
    }

    protected override object Build(RejectRapelRequest r, Caller caller) => new RejectRapelCommand(r.Id, r.Note, caller);
}

/// <summary>"Gaji Premi Saya" — self-only; lives under /api/overtime because Managers and Staff both reach it from Lembur.</summary>
public sealed class GetMyGajiPremiEndpoint(IMessageBus bus) : BusEndpoint<EmptyRequest, IReadOnlyList<MyGajiPremiRow>>(bus)
{
    public override void Configure()
    {
        Get("/gaji-premi/me");
        Group<OvertimeGroup>();
    }

    protected override object Build(EmptyRequest r, Caller caller) => new GetMyGajiPremiQuery(caller);
}

[Authorize]
public sealed class CreateRapelEndpoint(IMessageBus bus, ILeaveAttachmentStorage attachments)
    : Endpoint<CreateRapelRequest, RapelResult>
{
    public override void Configure()
    {
        Post("/rapel");
        Group<OvertimeGroup>();
        AllowFileUploads();
    }

    public override async Task HandleAsync(CreateRapelRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var attachment = await AttachmentUpload.SaveAsync(req.Attachment, attachments, ct);
        var result = await bus.InvokeAsync<Result<RapelResult>>(
            new CreateRapelCommand(req.AssignmentId, req.From, req.To, req.Note, req.Amount, attachment, caller), ct);

        if (attachment is not null && result is not Result<RapelResult>.Success)
        {
            await attachments.DeleteAsync(attachment.StorageKey, ct);
        }

        switch (result)
        {
            case Result<RapelResult>.Success s:
                await SendOkAsync(s.Value, ct);
                return;
            case Result<RapelResult>.NotFound:
                await SendNotFoundAsync(ct);
                return;
            case Result<RapelResult>.Error { Code: ResultErrors.Forbidden }:
                await SendForbiddenAsync(ct);
                return;
            case Result<RapelResult>.Error e:
                throw new DomainException(e.Code, e.Message);
        }
    }
}

[Authorize]
public sealed class GetRapelAttachmentEndpoint(IMessageBus bus) : Endpoint<OvertimeAttachmentRequest>
{
    public override void Configure()
    {
        Get("/rapel/{id:guid}/attachment");
        Group<OvertimeGroup>();
    }

    public override async Task HandleAsync(OvertimeAttachmentRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await AttachmentResponse.SendAsync(
            HttpContext.Response, await bus.InvokeAsync<Result<LeaveAttachmentContent>>(new GetRapelAttachmentQuery(req.Id, caller), ct), ct);
    }
}
