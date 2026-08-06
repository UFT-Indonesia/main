using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Devices.ListDevices;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance.Devices;

[Authorize(Roles = "Owner")]
public sealed class ListDevicesEndpoint : EndpointWithoutRequest<ListDevicesResponse>
{
    private readonly IMessageBus _bus;

    public ListDevicesEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/devices");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<ListDevicesResult>>(new ListDevicesQuery(), ct);

        if (result is Result<ListDevicesResult>.Success s)
        {
            await SendOkAsync(new ListDevicesResponse
            {
                Items = s.Value.Items.Select(AttendanceDeviceResponse.From).ToList(),
            }, ct);
            return;
        }

        if (result is Result<ListDevicesResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
