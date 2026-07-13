using System.Security.Claims;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Attendance.UpdateAttendancePolicy;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

[Authorize(Roles = "Owner,Manager")]
public sealed class UpdateAttendancePolicyEndpoint : Endpoint<UpdateAttendancePolicyRequest, AttendancePolicyResponse>
{
    private readonly IMessageBus _bus;

    public UpdateAttendancePolicyEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Put("/policy");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(UpdateAttendancePolicyRequest req, CancellationToken ct)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<AttendancePolicyResult>>(new UpdateAttendancePolicyCommand(
            req.ShiftStart,
            req.ShiftEnd,
            req.ClockInGraceMinutes,
            req.ClockOutGraceMinutes,
            req.TimeZoneId,
            userId), ct);

        if (result is Result<AttendancePolicyResult>.Success s)
        {
            await SendOkAsync(new AttendancePolicyResponse
            {
                ShiftStart = s.Value.ShiftStart,
                ShiftEnd = s.Value.ShiftEnd,
                ClockInGraceMinutes = s.Value.ClockInGraceMinutes,
                ClockOutGraceMinutes = s.Value.ClockOutGraceMinutes,
                TimeZoneId = s.Value.TimeZoneId,
                UpdatedByUserId = s.Value.UpdatedByUserId,
                UpdatedAtUtc = s.Value.UpdatedAtUtc,
            }, ct);
            return;
        }

        if (result is Result<AttendancePolicyResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<AttendancePolicyResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
