using System.Security.Claims;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.AddAttendanceLogNote;
using Erp.UseCases.Attendance.Common;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

[Authorize(Roles = "Owner,Manager")]
public sealed class AddAttendanceLogNoteEndpoint : Endpoint<AddAttendanceLogNoteRequest, AttendanceLogNoteResponse>
{
    private readonly IMessageBus _bus;

    public AddAttendanceLogNoteEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Post("/logs/{logId:guid}/notes");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(AddAttendanceLogNoteRequest req, CancellationToken ct)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var userName = User.FindFirstValue(ClaimTypes.Name) ?? "—";

        var result = await _bus.InvokeAsync<Result<AttendanceLogNoteResult>>(
            new AddAttendanceLogNoteCommand(req.LogId, req.Text, userId, userName), ct);

        if (result is Result<AttendanceLogNoteResult>.Success s)
        {
            await SendCreatedAtAsync<AddAttendanceLogNoteEndpoint>(
                null, AttendanceLogNoteResponse.From(s.Value), cancellation: ct);
            return;
        }

        if (result is Result<AttendanceLogNoteResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<AttendanceLogNoteResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
