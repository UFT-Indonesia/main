using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Devices.Common;
using Erp.UseCases.Attendance.Devices.SetDeviceEnabled;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance.Devices;

[Authorize(Roles = "Owner")]
public sealed class SetDeviceEnabledEndpoint : Endpoint<SetDeviceEnabledRequest>
{
    private readonly IMessageBus _bus;

    public SetDeviceEnabledEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Patch("/devices/{id}/enabled");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(SetDeviceEnabledRequest req, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<AttendanceDeviceResult>>(
            new SetDeviceEnabledCommand(req.Id, req.Enabled), ct);

        if (result is Result<AttendanceDeviceResult>.Success)
        {
            await SendNoContentAsync(ct);
            return;
        }

        if (result is Result<AttendanceDeviceResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<AttendanceDeviceResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
