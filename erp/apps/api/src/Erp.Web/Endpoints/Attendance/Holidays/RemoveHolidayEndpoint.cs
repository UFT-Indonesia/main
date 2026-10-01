using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Holidays.RemoveHoliday;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance.Holidays;

[Authorize(Roles = "Owner,Manager")]
public sealed class RemoveHolidayEndpoint : Endpoint<RemoveHolidayRequest>
{
    private readonly IMessageBus _bus;

    public RemoveHolidayEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Delete("/holidays/{date}");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(RemoveHolidayRequest req, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<bool>>(new RemoveHolidayCommand(req.Date), ct);

        if (result is Result<bool>.Success)
        {
            await SendNoContentAsync(ct);
            return;
        }

        if (result is Result<bool>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<bool>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
