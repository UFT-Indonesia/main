using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Employees.ListEmployeeAuditLog;

public sealed record ListEmployeeAuditLogQuery(
    int Page,
    int PageSize,
    IReadOnlyList<FilterRow> Filters,
    Caller Caller);
