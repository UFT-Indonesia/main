using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Wolverine;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeCreatedHandler
{
    // TODO: webhook / external-notify — no target system exists yet.
    public static async Task Handle(
        EmployeeCreated message,
        IRepository<EmployeeAuditLog> auditLogs,
        IReadRepository<Employee> employees,
        Envelope envelope,
        CancellationToken ct)
    {
        var parentName = await EmployeeAuditLogWriter.ResolveEmployeeNameAsync(employees, message.ParentId, ct);

        await EmployeeAuditLogWriter.WriteAsync(
            auditLogs,
            envelope,
            new EmployeeId(message.EmployeeId),
            message.EventType,
            message.RaisedAt,
            oldValue: null,
            newValue: new CreatedAuditValue(
                message.FullName,
                message.Nik,
                message.Npwp,
                message.Role.ToString(),
                message.MonthlyWage.Amount,
                message.MonthlyWage.Currency,
                message.EffectiveSalaryFrom.ToDateOnly(),
                message.ParentId,
                parentName),
            ct);
    }
}
