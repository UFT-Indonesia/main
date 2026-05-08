using Erp.SharedKernel.Domain;

namespace Erp.Core.Aggregates.Employees.Events;

/// <summary>
/// Emitted when an employee's parent (manager) reference changes.
/// </summary>
public sealed record EmployeeParentChanged(
    Guid EmployeeId,
    Guid? OldParentId,
    Guid? NewParentId)
    : DomainEvent(EmployeeId, nameof(Employee), "employee.parent_changed");
