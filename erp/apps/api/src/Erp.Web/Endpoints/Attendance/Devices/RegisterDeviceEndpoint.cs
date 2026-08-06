using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Devices.RegisterDevice;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance.Devices;

/// <summary>
/// Owner-only — a device's secret lets it punch attendance for any employee (no
/// employee-binding, since a shared reader must serve everyone), so registering one is as
/// sensitive as creating an account.
/// </summary>
[Authorize(Roles = "Owner")]
public sealed class RegisterDeviceEndpoint : Endpoint<RegisterDeviceRequest, RegisterDeviceResponse>
{
    private readonly IMessageBus _bus;

    public RegisterDeviceEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Post("/devices");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(RegisterDeviceRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<RegisterDeviceResult>>(
            new RegisterDeviceCommand(req.DeviceKey, req.Name, caller.UserId), ct);

        if (result is Result<RegisterDeviceResult>.Success s)
        {
            await SendAsync(RegisterDeviceResponse.From(s.Value), 201, cancellation: ct);
            return;
        }

        if (result is Result<RegisterDeviceResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
