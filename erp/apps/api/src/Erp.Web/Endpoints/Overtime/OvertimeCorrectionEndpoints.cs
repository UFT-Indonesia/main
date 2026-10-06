using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.GetLeaveAttachment;
using Erp.UseCases.Overtime.Common;
using Erp.UseCases.Overtime.Corrections;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Overtime;

/// <summary>Multipart, because the proof is required. <see cref="Time"/> is "HH:mm".</summary>
public sealed class CreateOvertimeCorrectionRequest
{
    public Guid AssignmentId { get; init; }
    public string Kind { get; init; } = default!;
    public string Time { get; init; } = default!;
    public string Reason { get; init; } = default!;
    public IFormFile? Attachment { get; init; }
}

public sealed class ListOvertimeCorrectionsRequest
{
    public string? Status { get; init; }
}

public sealed class DecideOvertimeCorrectionRequest
{
    public Guid Id { get; init; }
    public string? Note { get; init; }
}

public sealed class OvertimeAttachmentRequest
{
    public Guid Id { get; init; }
}

/// <summary>Only the employee files it, for their own overtime.</summary>
[Authorize]
public sealed class CreateOvertimeCorrectionEndpoint(IMessageBus bus, ILeaveAttachmentStorage attachments)
    : Endpoint<CreateOvertimeCorrectionRequest, OvertimeCorrectionResult>
{
    public override void Configure()
    {
        Post("/corrections");
        Group<OvertimeGroup>();
        AllowFileUploads();
    }

    public override async Task HandleAsync(CreateOvertimeCorrectionRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (!TimeOnly.TryParse(req.Time, out var time))
        {
            throw new DomainException("overtime_correction.time", "Time must be a valid HH:mm.");
        }

        var attachment = await AttachmentUpload.SaveAsync(req.Attachment, attachments, ct);
        var result = await bus.InvokeAsync<Result<OvertimeCorrectionResult>>(
            new CreateOvertimeCorrectionCommand(req.AssignmentId, req.Kind, time, req.Reason, attachment, caller), ct);

        // The file is on disk before the request is validated; anything short of success orphans it.
        if (attachment is not null && result is not Result<OvertimeCorrectionResult>.Success)
        {
            await attachments.DeleteAsync(attachment.StorageKey, ct);
        }

        switch (result)
        {
            case Result<OvertimeCorrectionResult>.Success s:
                await SendOkAsync(s.Value, ct);
                return;
            case Result<OvertimeCorrectionResult>.NotFound:
                await SendNotFoundAsync(ct);
                return;
            case Result<OvertimeCorrectionResult>.Error { Code: ResultErrors.Forbidden }:
                await SendForbiddenAsync(ct);
                return;
            case Result<OvertimeCorrectionResult>.Error e:
                throw new DomainException(e.Code, e.Message);
        }
    }
}

public sealed class ListOvertimeCorrectionsEndpoint(IMessageBus bus)
    : BusEndpoint<ListOvertimeCorrectionsRequest, IReadOnlyList<OvertimeCorrectionResult>>(bus)
{
    public override void Configure()
    {
        Get("/corrections");
        Group<OvertimeGroup>();
    }

    protected override object Build(ListOvertimeCorrectionsRequest r, Caller caller) =>
        new ListOvertimeCorrectionsQuery(r.Status, caller);
}

public sealed class ApproveOvertimeCorrectionEndpoint(IMessageBus bus)
    : BusEndpoint<DecideOvertimeCorrectionRequest, OvertimeCorrectionResult>(bus)
{
    public override void Configure()
    {
        Post("/corrections/{id:guid}/approve");
        Group<OvertimeGroup>();
    }

    protected override object Build(DecideOvertimeCorrectionRequest r, Caller caller) =>
        new DecideOvertimeCorrectionCommand(r.Id, Approve: true, r.Note, caller);
}

public sealed class RejectOvertimeCorrectionEndpoint(IMessageBus bus)
    : BusEndpoint<DecideOvertimeCorrectionRequest, OvertimeCorrectionResult>(bus)
{
    public override void Configure()
    {
        Post("/corrections/{id:guid}/reject");
        Group<OvertimeGroup>();
    }

    protected override object Build(DecideOvertimeCorrectionRequest r, Caller caller) =>
        new DecideOvertimeCorrectionCommand(r.Id, Approve: false, r.Note, caller);
}

/// <summary>Streams the proof — never a static file, so every read is a decision.</summary>
[Authorize]
public sealed class GetOvertimeCorrectionAttachmentEndpoint(IMessageBus bus) : Endpoint<OvertimeAttachmentRequest>
{
    public override void Configure()
    {
        Get("/corrections/{id:guid}/attachment");
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
            HttpContext.Response, await bus.InvokeAsync<Result<LeaveAttachmentContent>>(new GetOvertimeCorrectionAttachmentQuery(req.Id, caller), ct), ct);
    }
}

internal static class AttachmentResponse
{
    internal static async Task SendAsync(
        HttpResponse response, Result<LeaveAttachmentContent> result, CancellationToken ct)
    {
        switch (result)
        {
            case Result<LeaveAttachmentContent>.Success s:
                await using (var content = s.Value.Content)
                {
                    await response.SendStreamAsync(content, fileName: s.Value.FileName, contentType: s.Value.ContentType, cancellation: ct);
                }

                return;
            case Result<LeaveAttachmentContent>.NotFound:
                await response.SendNotFoundAsync(ct);
                return;
            case Result<LeaveAttachmentContent>.Error { Code: ResultErrors.Forbidden }:
                await response.SendForbiddenAsync(ct);
                return;
            case Result<LeaveAttachmentContent>.Error e:
                throw new DomainException(e.Code, e.Message);
        }
    }
}
