using Erp.UseCases.Employees.Common;

namespace Erp.UseCases.Employees.ListEmployeeAuditLog;

public sealed class ListEmployeeAuditLogResult
{
    public IReadOnlyList<EmployeeAuditLogEntryResult> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
