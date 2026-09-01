using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.GetLeaveAttachment;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

/// <summary>
/// Downloads the doctor's note on a Sick request. Behind the same authority as the request's
/// reason (LeaveRules.CanReadDetails) — never served as a static file, because the store holds
/// medical documents and every read of one has to be a decision, not a URL.
/// </summary>
[Authorize]
public sealed class GetLeaveAttachmentEndpoint : Endpoint<GetLeaveAttachmentRequest>
{
    private readonly IMessageBus _bus;

    public GetLeaveAttachmentEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/{Id}/attachment");
        Group<LeaveGroup>();
    }

    public override async Task HandleAsync(GetLeaveAttachmentRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<LeaveAttachmentContent>>(
            new GetLeaveAttachmentQuery(req.Id, caller), ct);

        if (result is Result<LeaveAttachmentContent>.Success s)
        {
            await using var content = s.Value.Content;
            await SendStreamAsync(
                content,
                fileName: s.Value.FileName,
                contentType: s.Value.ContentType,
                cancellation: ct);
            return;
        }

        if (result is Result<LeaveAttachmentContent>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<LeaveAttachmentContent>.Error e)
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
