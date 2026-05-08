using Erp.SharedKernel.Domain;

namespace Erp.Core.Aggregates.Employees.Events;

public sealed record EmployeeParentChanged(Guid EmployeeId, Guid? OldParentId, Guid? NewParentId)
    : DomainEvent;
