using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Attendance.GetAttendancePolicy;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

[Authorize]
public sealed class GetAttendancePolicyEndpoint : EndpointWithoutRequest<AttendancePolicyResponse>
{
    private readonly IMessageBus _bus;

    public GetAttendancePolicyEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/policy");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<AttendancePolicyResult>>(new GetAttendancePolicyQuery(), ct);

        if (result is Result<AttendancePolicyResult>.Success s)
        {
            await SendOkAsync(ToResponse(s.Value), ct);
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

    private static AttendancePolicyResponse ToResponse(AttendancePolicyResult result) => new()
    {
        ShiftStart = result.ShiftStart,
        ShiftEnd = result.ShiftEnd,
        ClockInGraceMinutes = result.ClockInGraceMinutes,
        ClockOutGraceMinutes = result.ClockOutGraceMinutes,
        TimeZoneId = result.TimeZoneId,
        UpdatedByUserId = result.UpdatedByUserId,
        UpdatedAtUtc = result.UpdatedAtUtc,
    };
}
