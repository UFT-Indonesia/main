using System.Text.Json;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using NodaTime;
using Wolverine;

namespace Erp.Infrastructure.DomainEventHandlers;

/// <summary>Shared write path for the EmployeeAuditLog*Handler classes.</summary>
internal static class EmployeeAuditLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(
        IRepository<EmployeeAuditLog> auditLogs,
        Envelope envelope,
        EmployeeId employeeId,
        string eventType,
        Instant occurredAtUtc,
        object? oldValue,
        object? newValue,
        CancellationToken ct)
    {
        var (actorUserId, actorName) = ReadActor(envelope);

        var entry = EmployeeAuditLog.Create(
            employeeId,
            eventType,
            occurredAtUtc,
            oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
            newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
            actorUserId,
            actorName);

        return auditLogs.AddAsync(entry, ct);
    }

    public static async Task<string?> ResolveEmployeeNameAsync(
        IReadRepository<Employee> employees, Guid? employeeId, CancellationToken ct) =>
        employeeId is { } id ? (await employees.GetByIdAsync(new EmployeeId(id), ct))?.FullName : null;

    /// <summary>
    /// Actor is stamped on the envelope by <c>EmployeeDomainEventPublisher</c> while the request
    /// is still alive. Missing headers mean no interactive caller (seeding, background jobs) —
    /// the row is still written, just unattributed.
    /// </summary>
    private static (Guid? ActorUserId, string? ActorName) ReadActor(Envelope envelope)
    {
        var actorUserId = envelope.TryGetHeader(EmployeeAuditHeaders.ActorUserId, out var rawUserId)
            && Guid.TryParse(rawUserId, out var parsed)
                ? parsed
                : (Guid?)null;

        var actorName = envelope.TryGetHeader(EmployeeAuditHeaders.ActorName, out var rawName)
            && !string.IsNullOrWhiteSpace(rawName)
                ? rawName
                : null;

        return (actorUserId, actorName);
    }
}

internal sealed record BasicInfoAuditValue(string FullName, string? Npwp);

internal sealed record SalaryAuditValue(decimal MonthlyWageAmount, string MonthlyWageCurrency, DateOnly EffectiveFrom);

internal sealed record ParentAuditValue(Guid? ParentId, string? ParentName);

internal sealed record RoleAuditValue(string Role);

internal sealed record TerminatedAuditValue(DateOnly TerminationDate);

internal sealed record HireDateAuditValue(DateOnly? HireDate);

internal sealed record ProbationEndAuditValue(DateOnly? ProbationEndsOn);

/// <summary>Null days means no override — the default entitlement applies.</summary>
internal sealed record LeaveQuotaAuditValue(string LeaveType, int? EntitledDays);

internal sealed record CreatedAuditValue(
    string FullName,
    string Nik,
    string? Npwp,
    string Role,
    decimal MonthlyWageAmount,
    string MonthlyWageCurrency,
    DateOnly EffectiveSalaryFrom,
    Guid? ParentId,
    string? ParentName,
    DateOnly? HireDate);
