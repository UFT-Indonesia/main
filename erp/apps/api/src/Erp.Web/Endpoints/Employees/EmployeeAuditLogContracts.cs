namespace Erp.Web.Endpoints.Employees;

public sealed class ListEmployeeAuditLogRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>Filter builder rows as a JSON array; see FilterApplier.Parse.</summary>
    public string? Filter { get; init; }
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
    /// <summary>The same rows the list endpoint took, so an export matches what is on screen.</summary>
    public string? Filter { get; init; }
}
