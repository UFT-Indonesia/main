using Erp.SharedKernel.Domain;

namespace Erp.Core.Aggregates.Employees.Events;

/// <summary>
/// Emitted when an <see cref="Employee"/>'s basic identifying information
/// (full name and/or NPWP) changes. Excludes salary, role, parent, and
/// status changes which have dedicated events.
/// </summary>
public sealed record EmployeeBasicInfoChanged(
    Guid EmployeeId,
    string OldFullName,
    string NewFullName,
    string? OldNpwp,
    string? NewNpwp)
    : DomainEvent(EmployeeId, nameof(Employee), "employee.basic_info_changed");
