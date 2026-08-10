using NodaTime;

namespace Erp.UseCases.Employees.Common;

public sealed class EmployeeAuditLogEntryResult
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public string EventType { get; init; } = default!;
    public Instant OccurredAtUtc { get; init; }
    public string? OldValueJson { get; init; }
    public string? NewValueJson { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorName { get; init; }
}
