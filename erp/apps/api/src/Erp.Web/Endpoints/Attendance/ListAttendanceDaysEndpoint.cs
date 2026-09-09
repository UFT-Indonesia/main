using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.ListAttendanceDays;
using Erp.UseCases.Common.Filtering;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

/// <summary>Staff see only their own days; Owner and Manager see the whole company.</summary>
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
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<ListAttendanceDaysResult>>(new ListAttendanceDaysQuery(
            req.Page,
            req.PageSize,
            FilterBinding.ParseOrThrow(req.Filter),
            caller), ct);

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
                    LeaveType = i.LeaveType,
                    LeaveStartDate = i.LeaveStartDate,
                    LeaveEndDate = i.LeaveEndDate,
                    LeaveWorkdayCount = i.LeaveWorkdayCount,
                    LeaveReason = i.LeaveReason,
                    LeaveRequestedAtUtc = i.LeaveRequestedAtUtc,
                    LeaveDecidedByName = i.LeaveDecidedByName,
                    LeaveDecidedAtUtc = i.LeaveDecidedAtUtc,
                    LeaveRequestId = i.LeaveRequestId,
                    LeaveAttachmentFileName = i.LeaveAttachmentFileName,
                    CanWrite = i.CanWrite,
                }).ToList(),
                Page = s.Value.Page,
                PageSize = s.Value.PageSize,
                TotalCount = s.Value.TotalCount,
            }, ct);
            return;
        }

        if (result is Result<ListAttendanceDaysResult>.Error e)
        {
            if (e.Code == FilterErrors.FieldForbidden)
            {
                await SendForbiddenAsync(ct);
                return;
            }

            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
