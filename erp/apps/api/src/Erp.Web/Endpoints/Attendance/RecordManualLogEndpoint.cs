using System.Security.Claims;
using Erp.UseCases.Attendance;
using Erp.SharedKernel.Domain.Errors;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

[Authorize]
public sealed class RecordManualLogEndpoint : Endpoint<ManualAttendanceLogRequest, AttendanceLogResponse>
{
    private readonly IMessageBus _bus;

    public RecordManualLogEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Post("/manual-logs");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(ManualAttendanceLogRequest req, CancellationToken ct)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Identifier claim is missing.");
        var userId = Guid.Parse(userIdValue);

        try
        {
            var result = await _bus.InvokeAsync<AttendanceResult>(new RecordManualAttendanceLog(
                req.EmployeeId,
                req.PunchedAtUtc,
                req.PunchType,
                userId,
                req.Note), ct);

            await SendCreatedAtAsync<RecordManualLogEndpoint>(
                null,
                ToResponse(result),
                cancellation: ct);
        }
        catch (KeyNotFoundException)
        {
            await SendNotFoundAsync(ct);
        }
        catch (DomainException ex)
        {
            ThrowError(ex.Message, 400);
        }
    }

    private static AttendanceLogResponse ToResponse(AttendanceResult result) => new()
    {
        Id = result.Id,
        EmployeeId = result.EmployeeId,
        PunchedAtUtc = result.PunchedAtUtc,
        Source = result.Source,
        PunchType = result.PunchType,
        DeviceId = result.DeviceId,
        RecordedByUserId = result.RecordedByUserId,
        Note = result.Note,
    };
}
