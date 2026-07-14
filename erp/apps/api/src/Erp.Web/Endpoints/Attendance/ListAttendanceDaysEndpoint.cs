using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.ListAttendanceDays;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

[Authorize]
public sealed class ListAttendanceDaysEndpoint : Endpoint<ListAttendanceDaysRequest, ListAttendanceDaysResponse>
{
    private readonly IMessageBus _bus;

    public ListAttendanceDaysEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/days");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(ListAttendanceDaysRequest req, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<ListAttendanceDaysResult>>(new ListAttendanceDaysQuery(
            req.Page,
            req.PageSize,
            req.EmployeeSearch,
            req.DateFrom,
            req.DateTo,
            req.Status), ct);

        if (result is Result<ListAttendanceDaysResult>.Success s)
        {
            await SendOkAsync(new ListAttendanceDaysResponse
            {
                Items = s.Value.Items.Select(i => new AttendanceDayListItemResponse
                {
                    EmployeeId = i.EmployeeId,
                    EmployeeFullName = i.EmployeeFullName,
                    Date = i.Date,
                    TapInUtc = i.TapInUtc,
                    TapOutUtc = i.TapOutUtc,
                    Status = i.Status,
                }).ToList(),
                Page = s.Value.Page,
                PageSize = s.Value.PageSize,
                TotalCount = s.Value.TotalCount,
            }, ct);
            return;
        }

        if (result is Result<ListAttendanceDaysResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
