using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.GetAttendanceDayLogs;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

[Authorize]
public sealed class GetAttendanceDayLogsEndpoint : Endpoint<GetAttendanceDayLogsRequest, GetAttendanceDayLogsResponse>
{
    private readonly IMessageBus _bus;

    public GetAttendanceDayLogsEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/days/{employeeId:guid}/{date}/logs");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(GetAttendanceDayLogsRequest req, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<GetAttendanceDayLogsResult>>(
            new GetAttendanceDayLogsQuery(req.EmployeeId, req.Date), ct);

        if (result is Result<GetAttendanceDayLogsResult>.Success s)
        {
            await SendOkAsync(new GetAttendanceDayLogsResponse
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
            }, ct);
            return;
        }

        if (result is Result<GetAttendanceDayLogsResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
