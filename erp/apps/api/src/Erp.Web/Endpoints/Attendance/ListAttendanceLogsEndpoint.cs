using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.ListAttendanceLogs;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

[Authorize]
public sealed class ListAttendanceLogsEndpoint : Endpoint<ListAttendanceLogsRequest, ListAttendanceLogsResponse>
{
    private readonly IMessageBus _bus;

    public ListAttendanceLogsEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(ListAttendanceLogsRequest req, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<ListAttendanceLogsResult>>(new ListAttendanceLogsQuery(
            req.Page,
            req.PageSize,
            req.EmployeeSearch,
            req.DateFrom,
            req.DateTo,
            req.PunchType,
            req.Source), ct);

        if (result is Result<ListAttendanceLogsResult>.Success s)
        {
            await SendOkAsync(new ListAttendanceLogsResponse
            {
                Items = s.Value.Items.Select(i => new AttendanceLogListItemResponse
                {
                    Id = i.Id,
                    EmployeeId = i.EmployeeId,
                    EmployeeFullName = i.EmployeeFullName,
                    PunchedAtUtc = i.PunchedAtUtc,
                    Source = i.Source,
                    PunchType = i.PunchType,
                    DeviceId = i.DeviceId,
                    RecordedByUserId = i.RecordedByUserId,
                    Note = i.Note,
                }).ToList(),
                Page = s.Value.Page,
                PageSize = s.Value.PageSize,
                TotalCount = s.Value.TotalCount,
            }, ct);
            return;
        }

        if (result is Result<ListAttendanceLogsResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
