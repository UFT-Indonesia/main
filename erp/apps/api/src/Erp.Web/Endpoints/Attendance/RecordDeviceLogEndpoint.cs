using System.Text.Json;
using Erp.Infrastructure.DeviceIngest;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Attendance.RecordDeviceLog;
using FastEndpoints;
using Microsoft.AspNetCore.Http.Features;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

public sealed class RecordDeviceLogEndpoint : EndpointWithoutRequest<AttendanceLogResponse>
{
    /// <summary>Generous for one punch's JSON — guards against a client streaming an unbounded body.</summary>
    private const long MaxBodyBytes = 8 * 1024;

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
        var bodySizeFeature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
        {
            bodySizeFeature.MaxRequestBodySize = MaxBodyBytes;
        }

        using var reader = new StreamReader(HttpContext.Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        DeviceAttendanceLogRequest? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<DeviceAttendanceLogRequest>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            throw new DomainException("attendance.invalid_json", "Attendance payload is not valid JSON.");
        }

        if (parsed is null)
        {
            throw new DomainException("attendance.invalid_payload", "Attendance payload must not be null.");
        }

        // The signature is still computed over the untouched raw body — parsing first only
        // reads which device claims to be signing it, so the right secret can be looked up.
        var signatureResult = await _signatureValidator.ValidateAsync(
            payload,
            parsed.DeviceId,
            HttpContext.Request.Headers["X-Device-Timestamp"].FirstOrDefault(),
            HttpContext.Request.Headers["X-Device-Signature"].FirstOrDefault(),
            ct);

        if (!signatureResult.IsValid)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<AttendanceResult>>(new RecordDeviceLogCommand(
            parsed.EmployeeId,
            parsed.PunchedAtUtc,
            parsed.PunchType,
            parsed.DeviceId), ct);

        if (result is Result<AttendanceResult>.Success s)
        {
            await SendAsync(ToResponse(s.Value), 201, cancellation: ct);
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
        Notes = AttendanceLogNoteResponse.FromAll(result.Notes),
    };
}
