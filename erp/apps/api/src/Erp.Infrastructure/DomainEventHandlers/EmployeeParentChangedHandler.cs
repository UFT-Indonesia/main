using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Wolverine;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeParentChangedHandler
{
    // No standalone org-chart projection needed — Employee.ParentId is the live reporting
    // structure already, recalculated via EmployeeHierarchyService.
    public static async Task Handle(
        EmployeeParentChanged message,
        IRepository<EmployeeAuditLog> auditLogs,
        IReadRepository<Employee> employees,
        Envelope envelope,
        CancellationToken ct)
    {
        var oldParentName = await EmployeeAuditLogWriter.ResolveEmployeeNameAsync(employees, message.OldParentId, ct);
        var newParentName = await EmployeeAuditLogWriter.ResolveEmployeeNameAsync(employees, message.NewParentId, ct);

        await EmployeeAuditLogWriter.WriteAsync(
            auditLogs,
            envelope,
            new EmployeeId(message.EmployeeId),
            message.EventType,
            message.RaisedAt,
            oldValue: new ParentAuditValue(message.OldParentId, oldParentName),
            newValue: new ParentAuditValue(message.NewParentId, newParentName),
            ct);
    }
}
