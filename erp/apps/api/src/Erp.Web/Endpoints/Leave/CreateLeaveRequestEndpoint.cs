using System.Security.Claims;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.CreateLeaveRequest;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

/// <summary>
/// Scoped by <see cref="LeaveRules.CanFileFor"/>: Staff file for themselves, a Manager for
/// themselves or their own Staff, an Owner for anyone. An Owner's own leave is approved on
/// creation — nobody outranks them to decide it.
/// </summary>
[Authorize]
public sealed class CreateLeaveRequestEndpoint : Endpoint<CreateLeaveRequestRequest, LeaveRequestResponse>
{
    private readonly IMessageBus _bus;
    private readonly ILeaveAttachmentStorage _attachments;

    public CreateLeaveRequestEndpoint(IMessageBus bus, ILeaveAttachmentStorage attachments)
    {
        _bus = bus;
        _attachments = attachments;
    }

    public override void Configure()
    {
        Post("/");
        Group<LeaveGroup>();
        // Sick leave carries a doctor's note, so this one endpoint takes a file alongside its
        // fields. The cap is enforced here as well as in the domain: the domain check only runs
        // after the bytes are already on disk, which is too late to be a limit.
        AllowFileUploads();
    }

    public override async Task HandleAsync(CreateLeaveRequestRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        LeaveAttachment? attachment = null;
        if (req.Attachment is { Length: > 0 } upload)
        {
            if (upload.Length > LeaveRequest.AttachmentMaxBytes)
            {
                throw new DomainException(
                    "leave.attachment_too_large",
                    $"The file exceeds the {LeaveRequest.AttachmentMaxBytes / (1024 * 1024)}MB limit.");
            }

            if (!LeaveRequest.AllowedAttachmentContentTypes.Contains(upload.ContentType))
            {
                throw new DomainException(
                    "leave.attachment_type", "The file must be a PDF, JPEG, or PNG.");
            }

            await using var stream = upload.OpenReadStream();
            var storageKey = await _attachments.SaveAsync(stream, upload.FileName, ct);
            attachment = LeaveAttachment.Create(
                storageKey, upload.FileName, upload.ContentType, upload.Length);
        }

        var result = await _bus.InvokeAsync<Result<LeaveRequestResult>>(new CreateLeaveRequestCommand(
            req.EmployeeId,
            req.Type,
            req.StartDate,
            req.EndDate,
            req.Reason,
            attachment,
            caller), ct);

        // The file is written before the request is validated, so anything short of success
        // leaves it orphaned on disk with no row pointing at it.
        if (attachment is not null && result is not Result<LeaveRequestResult>.Success)
        {
            await _attachments.DeleteAsync(attachment.StorageKey, ct);
        }

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
