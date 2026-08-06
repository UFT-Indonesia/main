using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Attendance.GetAttendanceDayLogs;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

/// <summary>Staff may open only their own day; Owner and Manager may open anyone's.</summary>
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
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<GetAttendanceDayLogsResult>>(
            new GetAttendanceDayLogsQuery(req.EmployeeId, req.Date, caller), ct);

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
                    Notes = AttendanceLogNoteResponse.FromAll(i.Notes),
                    CanWrite = i.CanWrite,
                }).ToList(),
            }, ct);
            return;
        }

        if (result is Result<GetAttendanceDayLogsResult>.Error e)
        {
            if (e.Code == ResultErrors.Forbidden)
            {
                await SendForbiddenAsync(ct);
                return;
            }

            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
