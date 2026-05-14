using System.Text.Json;
using Erp.Infrastructure.DeviceIngest;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Attendance.RecordDeviceLog;
using FastEndpoints;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

public sealed class RecordDeviceLogEndpoint : EndpointWithoutRequest<AttendanceLogResponse>
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

    public override async Task HandleAsync(CancellationToken ct)
    {
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

        var result = await _bus.InvokeAsync<Result<AttendanceResult>>(new RecordDeviceLogCommand(
            parsed.EmployeeId,
            parsed.PunchedAtUtc,
            parsed.PunchType,
            parsed.DeviceId), ct);

        if (result is Result<AttendanceResult>.Success s)
        {
            await SendCreatedAtAsync<RecordDeviceLogEndpoint>(
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
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsJsonAsync(new { code = e.Code, message = e.Message }, ct);
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
