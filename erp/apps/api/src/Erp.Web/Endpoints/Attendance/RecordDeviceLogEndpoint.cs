using System.Text.Json;
using Erp.Infrastructure.DeviceIngest;
using Erp.SharedKernel.Domain.Errors;
using Erp.UseCases.Attendance;
using FastEndpoints;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

public sealed class RecordDeviceLogEndpoint : Endpoint<DeviceAttendanceLogRequest, AttendanceLogResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDeviceIngestSignatureValidator _signatureValidator;
    private readonly IMessageBus _bus;

    public RecordDeviceLogEndpoint(
        IDeviceIngestSignatureValidator signatureValidator,
        IMessageBus bus)
    {
        _signatureValidator = signatureValidator;
        _bus = bus;
    }

    public override void Configure()
    {
        Post("/device-logs");
        AllowAnonymous();
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(DeviceAttendanceLogRequest req, CancellationToken ct)
    {
        HttpContext.Request.EnableBuffering();
        HttpContext.Request.Body.Position = 0;

        using var reader = new StreamReader(HttpContext.Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        var signatureResult = _signatureValidator.Validate(
            payload,
            HttpContext.Request.Headers["X-Device-Timestamp"].FirstOrDefault(),
            HttpContext.Request.Headers["X-Device-Signature"].FirstOrDefault());

        if (!signatureResult.IsValid)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var parsed = JsonSerializer.Deserialize<DeviceAttendanceLogRequest>(payload, JsonOptions);
        if (parsed is null)
        {
            ThrowError("Invalid attendance payload.", 400);
        }

        try
        {
            var result = await _bus.InvokeAsync<AttendanceResult>(new RecordDeviceAttendanceLog(
                parsed.EmployeeId,
                parsed.PunchedAtUtc,
                parsed.PunchType,
                parsed.DeviceId), ct);

            await SendCreatedAtAsync<RecordDeviceLogEndpoint>(
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
