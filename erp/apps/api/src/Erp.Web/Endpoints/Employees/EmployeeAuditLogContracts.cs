namespace Erp.Web.Endpoints.Employees;

public sealed class ListEmployeeAuditLogRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? EmployeeId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public string? EventType { get; init; }
}

public sealed class EmployeeAuditLogEntryResponse
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public string EventType { get; init; } = default!;
    public DateTimeOffset OccurredAtUtc { get; init; }
    public string? OldValueJson { get; init; }
    public string? NewValueJson { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorName { get; init; }
}

public sealed class ListEmployeeAuditLogResponse
{
    public IReadOnlyList<EmployeeAuditLogEntryResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class ExportEmployeeAuditLogRequest
{
    public Guid? EmployeeId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public string? EventType { get; init; }
}
