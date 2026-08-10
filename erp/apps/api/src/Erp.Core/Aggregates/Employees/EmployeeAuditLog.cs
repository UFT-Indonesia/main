using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Employees;

/// <summary>
/// Append-only audit row for an Employee change (created / basic info / salary / role /
/// parent / terminated). Not an aggregate root — no domain events, nothing else mutates it.
/// Old/new values are stored as JSON so one table shape covers every event type without a
/// migration each time a new audited field appears; names embedded in the row (actor, parent)
/// are snapshotted at write time so a later rename/deletion doesn't rewrite history.
/// <para>
/// <see cref="ActorUserId"/> is null for changes with no interactive caller (seeding,
/// background jobs) — the row still records what changed, just not who.
/// </para>
/// </summary>
public sealed class EmployeeAuditLog : Entity
{
    // EF Core constructor.
    private EmployeeAuditLog() { }

    private EmployeeAuditLog(
        Guid id,
        EmployeeId employeeId,
        string eventType,
        Instant occurredAtUtc,
        string? oldValueJson,
        string? newValueJson,
        Guid? actorUserId,
        string? actorName)
        : base(id)
    {
        EmployeeId = employeeId;
        EventType = eventType;
        OccurredAtUtc = occurredAtUtc;
        OldValueJson = oldValueJson;
        NewValueJson = newValueJson;
        ActorUserId = actorUserId;
        ActorName = actorName;
    }

    public EmployeeId EmployeeId { get; private set; }

    public string EventType { get; private set; } = default!;

    public Instant OccurredAtUtc { get; private set; }

    public string? OldValueJson { get; private set; }

    public string? NewValueJson { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string? ActorName { get; private set; }

    public static EmployeeAuditLog Create(
        EmployeeId employeeId,
        string eventType,
        Instant occurredAtUtc,
        string? oldValueJson,
        string? newValueJson,
        Guid? actorUserId = null,
        string? actorName = null) =>
        new(Guid.NewGuid(), employeeId, eventType, occurredAtUtc, oldValueJson, newValueJson, actorUserId, actorName);
}
