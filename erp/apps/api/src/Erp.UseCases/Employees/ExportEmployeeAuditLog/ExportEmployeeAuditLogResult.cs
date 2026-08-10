namespace Erp.UseCases.Employees.ExportEmployeeAuditLog;

public sealed class ExportEmployeeAuditLogRowResult
{
    public string EmployeeFullName { get; init; } = default!;
    public string EventType { get; init; } = default!;
    public string ActorName { get; init; } = default!;
    public string OccurredAtUtc { get; init; } = default!;
    public string OldValueJson { get; init; } = default!;
    public string NewValueJson { get; init; } = default!;
}

public sealed class ExportEmployeeAuditLogResult
{
    public IReadOnlyList<ExportEmployeeAuditLogRowResult> Rows { get; init; } = [];
}
