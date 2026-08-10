using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Wolverine;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeTerminatedHandler
{
    // Employee.Status shows *that* someone was terminated; only this row shows who did it and when.
    public static Task Handle(
        EmployeeTerminated message,
        IRepository<EmployeeAuditLog> auditLogs,
        Envelope envelope,
        CancellationToken ct) =>
        EmployeeAuditLogWriter.WriteAsync(
            auditLogs,
            envelope,
            new EmployeeId(message.EmployeeId),
            message.EventType,
            message.RaisedAt,
            oldValue: null,
            newValue: new TerminatedAuditValue(message.TerminationDate.ToDateOnly()),
            ct);
}
