using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Wolverine;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeSalaryChangedHandler
{
    // TODO: payroll/accounting integration — no target system exists yet.
    public static Task Handle(
        EmployeeSalaryChanged message,
        IRepository<EmployeeAuditLog> auditLogs,
        Envelope envelope,
        CancellationToken ct) =>
        EmployeeAuditLogWriter.WriteAsync(
            auditLogs,
            envelope,
            new EmployeeId(message.EmployeeId),
            message.EventType,
            message.RaisedAt,
            oldValue: new SalaryAuditValue(
                message.OldMonthlyWage.Amount, message.OldMonthlyWage.Currency, message.OldEffectiveFrom.ToDateOnly()),
            newValue: new SalaryAuditValue(
                message.NewMonthlyWage.Amount, message.NewMonthlyWage.Currency, message.NewEffectiveFrom.ToDateOnly()),
            ct);
}
