using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Employees.ExportEmployeeAuditLog;

public sealed record ExportEmployeeAuditLogQuery(
    IReadOnlyList<FilterRow> Filters,
    Caller Caller);
