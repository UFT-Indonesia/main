using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.Common;

namespace Erp.UseCases.Employees.ExportEmployeeAuditLog;

public static class ExportEmployeeAuditLogHandler
{
    internal const int MaxRows = 10_000;

    public static async Task<Result<ExportEmployeeAuditLogResult>> Handle(
        ExportEmployeeAuditLogQuery query,
        IReadRepository<EmployeeAuditLog> auditLogs,
        IReadRepository<Employee> employees,
        CancellationToken ct)
    {
        var totalCount = await auditLogs.CountAsync(
            new EmployeeAuditLogCountSpec(query.EmployeeId, query.DateFrom, query.DateTo, query.EventType), ct);

        if (totalCount > MaxRows)
        {
            return new Result<ExportEmployeeAuditLogResult>.Error(
                "employee_audit_log.export_too_many",
                $"Cannot export more than {MaxRows} rows at once — narrow your filters.");
        }

        var items = await auditLogs.ListAsync(
            new EmployeeAuditLogExportSpec(query.EmployeeId, query.DateFrom, query.DateTo, query.EventType), ct);

        var nameById = await EmployeeAuditLogNameResolver.ResolveAsync(
            employees, items.Select(log => log.EmployeeId), ct);

        var rows = items.Select(log => new ExportEmployeeAuditLogRowResult
        {
            EmployeeFullName = nameById.GetValueOrDefault(log.EmployeeId, "—"),
            EventType = log.EventType,
            ActorName = log.ActorName ?? "—",
            OccurredAtUtc = log.OccurredAtUtc.ToString(),
            OldValueJson = log.OldValueJson ?? string.Empty,
            NewValueJson = log.NewValueJson ?? string.Empty,
        }).ToList();

        return new Result<ExportEmployeeAuditLogResult>.Success(
            new ExportEmployeeAuditLogResult { Rows = rows });
    }
}
