using Erp.SharedKernel.Domain;

namespace Erp.Core.Aggregates.Employees.Events;

/// <summary>
/// Emitted when an employee's role transitions (Owner / Manager / Staff).
/// </summary>
public sealed record EmployeeRoleChanged(
    Guid EmployeeId,
    EmployeeRole OldRole,
    EmployeeRole NewRole)
    : DomainEvent(EmployeeId, nameof(Employee), "employee.role_changed");
