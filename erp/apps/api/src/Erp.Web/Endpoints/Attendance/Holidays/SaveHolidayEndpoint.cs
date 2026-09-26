using System.Security.Claims;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Holidays.Common;
using Erp.UseCases.Attendance.Holidays.SaveHoliday;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance.Holidays;

/// <summary>Same audience as the attendance policy: a holiday moves everyone's quota and absences.</summary>
[Authorize(Roles = "Owner,Manager")]
public sealed class SaveHolidayEndpoint : Endpoint<SaveHolidayRequest, HolidayResponse>
{
    private readonly IMessageBus _bus;

    public SaveHolidayEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Put("/holidays/{date}");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(SaveHolidayRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<HolidayResult>>(
            new SaveHolidayCommand(req.Date, req.Name, req.Kind, userId), ct);

        if (result is Result<HolidayResult>.Success s)
        {
            await SendOkAsync(HolidayResponse.From(s.Value), ct);
            return;
        }

        if (result is Result<HolidayResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
