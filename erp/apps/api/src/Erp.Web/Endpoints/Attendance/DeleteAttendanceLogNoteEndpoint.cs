using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Attendance.DeleteAttendanceLogNote;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

/// <summary>Owner writes for anyone; a Manager only for themselves and their direct Staff (enforced per-request by AttendanceRules).</summary>
[Authorize(Roles = "Owner,Manager")]
public sealed class DeleteAttendanceLogNoteEndpoint : Endpoint<DeleteAttendanceLogNoteRequest>
{
    private readonly IMessageBus _bus;

    public DeleteAttendanceLogNoteEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Delete("/logs/{logId:guid}/notes/{noteId:guid}");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(DeleteAttendanceLogNoteRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<bool>>(
            new DeleteAttendanceLogNoteCommand(req.LogId, req.NoteId, caller), ct);

        if (result is Result<bool>.Success)
        {
            await SendNoContentAsync(ct);
            return;
        }

        if (result is Result<bool>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<bool>.Error e)
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
