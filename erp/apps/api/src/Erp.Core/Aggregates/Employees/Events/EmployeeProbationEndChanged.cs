using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Employees.Events;

/// <summary>
/// Emitted when an employee's probation end moves — by an Owner's direct edit or by an approved
/// extension request. Carries the *effective* dates (override applied), not the raw override, so
/// the audit trail reads as what actually changed for the employee.
/// </summary>
public sealed record EmployeeProbationEndChanged(
    Guid EmployeeId,
    LocalDate? OldProbationEndsOn,
    LocalDate? NewProbationEndsOn)
    : DomainEvent(EmployeeId, nameof(Employee), "employee.probation_end_changed");
