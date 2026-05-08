using Erp.SharedKernel.Domain;

namespace Erp.Core.Aggregates.Employees.Events;

public sealed record EmployeeRoleChanged(Guid EmployeeId, EmployeeRole OldRole, EmployeeRole NewRole)
    : DomainEvent;
