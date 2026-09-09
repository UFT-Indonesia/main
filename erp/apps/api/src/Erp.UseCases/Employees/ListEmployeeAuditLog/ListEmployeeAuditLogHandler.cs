using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common.Filtering;
using Erp.UseCases.Employees.Common;

namespace Erp.UseCases.Employees.ListEmployeeAuditLog;

public static class ListEmployeeAuditLogHandler
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static async Task<Result<ListEmployeeAuditLogResult>> Handle(
        ListEmployeeAuditLogQuery query,
        IReadRepository<EmployeeAuditLog> auditLogs,
        IReadRepository<Employee> employees,
        CancellationToken ct)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

        if (!FilterApplier.TryCompile(EmployeeAuditLogFilterFields.Fields, query.Filters, query.Caller, out var filters, out var failure))
        {
            return new Result<ListEmployeeAuditLogResult>.Error(failure.Code, failure.Message);
        }

        var totalCount = await auditLogs.CountAsync(new EmployeeAuditLogCountSpec(filters), ct);
        var items = await auditLogs.ListAsync(new EmployeeAuditLogListSpec(page, pageSize, filters), ct);

        var nameById = await EmployeeAuditLogNameResolver.ResolveAsync(
            employees, items.Select(log => log.EmployeeId), ct);

        var resultItems = items.Select(log => new EmployeeAuditLogEntryResult
        {
            Id = log.Id,
            EmployeeId = log.EmployeeId.Value,
            EmployeeFullName = nameById.GetValueOrDefault(log.EmployeeId, "—"),
            EventType = log.EventType,
            OccurredAtUtc = log.OccurredAtUtc,
            OldValueJson = log.OldValueJson,
            NewValueJson = log.NewValueJson,
            ActorUserId = log.ActorUserId,
            ActorName = log.ActorName,
        }).ToList();

        return new Result<ListEmployeeAuditLogResult>.Success(new ListEmployeeAuditLogResult
        {
            Items = resultItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        });
    }
}
