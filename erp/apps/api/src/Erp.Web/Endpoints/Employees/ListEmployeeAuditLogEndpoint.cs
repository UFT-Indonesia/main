using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.ListEmployeeAuditLog;
using Erp.UseCases.Common.Filtering;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Employees;

[Authorize(Roles = "Owner")]
public sealed class ListEmployeeAuditLogEndpoint : Endpoint<ListEmployeeAuditLogRequest, ListEmployeeAuditLogResponse>
{
    private readonly IMessageBus _bus;

    public ListEmployeeAuditLogEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/audit-log");
        Group<EmployeeGroup>();
    }

    public override async Task HandleAsync(ListEmployeeAuditLogRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<ListEmployeeAuditLogResult>>(new ListEmployeeAuditLogQuery(
            req.Page,
            req.PageSize,
            FilterBinding.ParseOrThrow(req.Filter),
            caller), ct);

        if (result is Result<ListEmployeeAuditLogResult>.Success s)
        {
            await SendOkAsync(new ListEmployeeAuditLogResponse
            {
                Items = s.Value.Items.Select(ToResponse).ToList(),
                Page = s.Value.Page,
                PageSize = s.Value.PageSize,
                TotalCount = s.Value.TotalCount,
            }, ct);
            return;
        }

        if (result is Result<ListEmployeeAuditLogResult>.Error e)
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

    private static EmployeeAuditLogEntryResponse ToResponse(EmployeeAuditLogEntryResult item) => new()
    {
        Id = item.Id,
        EmployeeId = item.EmployeeId,
        EmployeeFullName = item.EmployeeFullName,
        EventType = item.EventType,
        OccurredAtUtc = item.OccurredAtUtc.ToDateTimeOffset(),
        OldValueJson = item.OldValueJson,
        NewValueJson = item.NewValueJson,
        ActorUserId = item.ActorUserId,
        ActorName = item.ActorName,
    };
}
