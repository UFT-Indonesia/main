using Erp.Core.Aggregates.Common;
using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Employees.Events;

/// <summary>
/// Emitted when an employee's monthly wage is updated. Includes the previous
/// values to enable delta-style projections without a prior read.
/// </summary>
public sealed record EmployeeSalaryChanged(
    Guid EmployeeId,
    Money OldMonthlyWage,
    LocalDate OldEffectiveFrom,
    Money NewMonthlyWage,
    LocalDate NewEffectiveFrom)
    : DomainEvent(EmployeeId, nameof(Employee), "employee.salary_changed");
