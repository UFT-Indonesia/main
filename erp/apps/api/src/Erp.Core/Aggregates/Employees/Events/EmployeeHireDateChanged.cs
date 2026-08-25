using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Employees.Events;

/// <summary>
/// Emitted when an employee's hire date is set or corrected. The hire date anchors the default
/// probation end, so a change here can move someone's leave entitlement — hence the audit row.
/// </summary>
public sealed record EmployeeHireDateChanged(
    Guid EmployeeId,
    LocalDate? OldHireDate,
    LocalDate? NewHireDate)
    : DomainEvent(EmployeeId, nameof(Employee), "employee.hire_date_changed");
