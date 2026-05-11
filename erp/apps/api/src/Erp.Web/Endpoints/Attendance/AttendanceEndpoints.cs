using System.Security.Claims;
using System.Text.Json;
using Erp.Core.Aggregates.Attendance;
using Erp.Infrastructure.DeviceIngest;
using Erp.Infrastructure.Persistence;
using Erp.SharedKernel.Domain.Errors;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Erp.Web.Endpoints.Attendance;

public static class AttendanceEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendance").WithTags("Attendance");

        group.MapPost("/device-logs", RecordDeviceLogAsync).AllowAnonymous();
        group.MapPost("/manual-logs", RecordManualLogAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> RecordDeviceLogAsync(
        HttpRequest httpRequest,
        IDeviceIngestSignatureValidator signatureValidator,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(httpRequest.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signatureResult = signatureValidator.Validate(
            payload,
            httpRequest.Headers["X-Device-Timestamp"].FirstOrDefault(),
            httpRequest.Headers["X-Device-Signature"].FirstOrDefault());

        if (!signatureResult.IsValid)
        {
            return Results.Unauthorized();
        }

        var request = JsonSerializer.Deserialize<DeviceAttendanceLogRequest>(payload, JsonOptions);
        if (request is null)
        {
            return Results.BadRequest(new { code = "attendance.invalid_json", message = "Invalid attendance payload." });
        }

        if (!TryParsePunchType(request.PunchType, out var punchType))
        {
            return Results.BadRequest(new { code = "attendance.punch_type", message = "Punch type must be In or Out." });
        }

        if (!await dbContext.Employees.AnyAsync(employee => employee.Id == request.EmployeeId, cancellationToken))
        {
            return Results.NotFound(new { code = "attendance.employee_not_found", message = "Employee was not found." });
        }

        try
        {
            var log = AttendanceLog.FromDevice(
                request.EmployeeId,
                Instant.FromDateTimeOffset(request.PunchedAtUtc),
                punchType,
                request.DeviceId);

            dbContext.AttendanceLogs.Add(log);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/attendance/logs/{log.Id}", ToResponse(log));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }

    private static async Task<IResult> RecordManualLogAsync(
        ManualAttendanceLogRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Results.Unauthorized();
        }

        if (!TryParsePunchType(request.PunchType, out var punchType))
        {
            return Results.BadRequest(new { code = "attendance.punch_type", message = "Punch type must be In or Out." });
        }

        if (!await dbContext.Employees.AnyAsync(employee => employee.Id == request.EmployeeId, cancellationToken))
        {
            return Results.NotFound(new { code = "attendance.employee_not_found", message = "Employee was not found." });
        }

        try
        {
            var log = AttendanceLog.Manual(
                request.EmployeeId,
                Instant.FromDateTimeOffset(request.PunchedAtUtc),
                punchType,
                userId,
                request.Note);

            dbContext.AttendanceLogs.Add(log);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/attendance/logs/{log.Id}", ToResponse(log));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }

    private static bool TryParsePunchType(string value, out PunchType punchType) =>
        Enum.TryParse(value, ignoreCase: true, out punchType);

    private static AttendanceLogResponse ToResponse(AttendanceLog log) =>
        new(
            log.Id,
            log.EmployeeId,
            log.PunchedAtUtc.ToDateTimeOffset(),
            log.Source.ToString(),
            log.PunchType.ToString(),
            log.DeviceId,
            log.RecordedByUserId,
            log.Note);
}
