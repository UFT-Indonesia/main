using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Wolverine;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeHireDateChangedHandler
{
    public static Task Handle(
        EmployeeHireDateChanged message,
        IRepository<EmployeeAuditLog> auditLogs,
        Envelope envelope,
        CancellationToken ct) =>
        EmployeeAuditLogWriter.WriteAsync(
            auditLogs,
            envelope,
            new EmployeeId(message.EmployeeId),
            message.EventType,
            message.RaisedAt,
            oldValue: new HireDateAuditValue(message.OldHireDate?.ToDateOnly()),
            newValue: new HireDateAuditValue(message.NewHireDate?.ToDateOnly()),
            ct);
}

public static class EmployeeProbationEndChangedHandler
{
    public static Task Handle(
        EmployeeProbationEndChanged message,
        IRepository<EmployeeAuditLog> auditLogs,
        Envelope envelope,
        CancellationToken ct) =>
        EmployeeAuditLogWriter.WriteAsync(
            auditLogs,
            envelope,
            new EmployeeId(message.EmployeeId),
            message.EventType,
            message.RaisedAt,
            oldValue: new ProbationEndAuditValue(message.OldProbationEndsOn?.ToDateOnly()),
            newValue: new ProbationEndAuditValue(message.NewProbationEndsOn?.ToDateOnly()),
            ct);
}

public static class EmployeeLeaveQuotaChangedHandler
{
    public static Task Handle(
        EmployeeLeaveQuotaChanged message,
        IRepository<EmployeeAuditLog> auditLogs,
        Envelope envelope,
        CancellationToken ct) =>
        EmployeeAuditLogWriter.WriteAsync(
            auditLogs,
            envelope,
            new EmployeeId(message.EmployeeId),
            message.EventType,
            message.RaisedAt,
            oldValue: new LeaveQuotaAuditValue(message.Type.ToString(), message.OldEntitledDays),
            newValue: new LeaveQuotaAuditValue(message.Type.ToString(), message.NewEntitledDays),
            ct);
}
