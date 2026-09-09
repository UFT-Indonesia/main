using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Employees.Common;

/// <summary>
/// What the Log Audit Karyawan filter builder may ask about. The whole endpoint is already
/// Owner-only, so no field needs a visibility rule of its own.
/// </summary>
public static class EmployeeAuditLogFilterFields
{
    public static readonly FilterFieldMap<EmployeeAuditLog> Fields = new FilterFieldMap<EmployeeAuditLog>()
        .Relation("employeeId", log => log.EmployeeId, id => new EmployeeId(id))
        .Choice("eventType", log => log.EventType)
        .Timestamp("occurredAt", log => log.OccurredAtUtc)
        .Text("actorName", log => log.ActorName, nullable: true);
}
