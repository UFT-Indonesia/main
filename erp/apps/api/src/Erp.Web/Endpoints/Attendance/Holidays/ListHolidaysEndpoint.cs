using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Holidays.Common;
using Erp.UseCases.Attendance.Holidays.ListHolidays;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance.Holidays;

/// <summary>Everyone reads the calendar — Staff need it to see why a leave day wasn't charged.</summary>
[Authorize]
public sealed class ListHolidaysEndpoint : Endpoint<ListHolidaysRequest, List<HolidayResponse>>
{
    private readonly IMessageBus _bus;

    public ListHolidaysEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/holidays");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(ListHolidaysRequest req, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<IReadOnlyList<HolidayResult>>>(
            new ListHolidaysQuery(req.From, req.To), ct);

        if (result is Result<IReadOnlyList<HolidayResult>>.Success s)
        {
            await SendOkAsync(s.Value.Select(HolidayResponse.From).ToList(), ct);
            return;
        }

        if (result is Result<IReadOnlyList<HolidayResult>>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
