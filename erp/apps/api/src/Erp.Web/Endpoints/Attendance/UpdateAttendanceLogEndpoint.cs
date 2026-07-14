using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Attendance.UpdateAttendanceLog;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

[Authorize(Roles = "Owner,Manager")]
public sealed class UpdateAttendanceLogEndpoint : Endpoint<UpdateAttendanceLogRequest, AttendanceLogResponse>
{
    private readonly IMessageBus _bus;

    public UpdateAttendanceLogEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Patch("/logs/{id:guid}");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(UpdateAttendanceLogRequest req, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<AttendanceResult>>(new UpdateAttendanceLogCommand(
            req.Id,
            req.PunchedAtUtc,
            req.PunchType), ct);

        if (result is Result<AttendanceResult>.Success s)
        {
            await SendOkAsync(new AttendanceLogResponse
            {
                Id = s.Value.Id,
                EmployeeId = s.Value.EmployeeId,
                PunchedAtUtc = s.Value.PunchedAtUtc,
                Source = s.Value.Source,
                PunchType = s.Value.PunchType,
                DeviceId = s.Value.DeviceId,
                RecordedByUserId = s.Value.RecordedByUserId,
                Notes = AttendanceLogNoteResponse.FromAll(s.Value.Notes),
            }, ct);
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
}
