using System.Security.Claims;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Attendance.RecordManualLog;
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
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<AttendanceResult>>(new RecordManualLogCommand(
            req.EmployeeId,
            req.PunchedAtUtc,
            req.PunchType,
            userId,
            req.Note), ct);

        if (result is Result<AttendanceResult>.Success s)
        {
            await SendCreatedAtAsync<RecordManualLogEndpoint>(
                null,
                ToResponse(s.Value),
                cancellation: ct);
            return;
        }

        if (result is Result<AttendanceResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<AttendanceResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
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
