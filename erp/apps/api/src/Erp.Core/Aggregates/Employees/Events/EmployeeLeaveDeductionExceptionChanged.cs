using Erp.SharedKernel.Domain;

namespace Erp.Core.Aggregates.Employees.Events;

/// <summary>
/// Emitted when an Owner sets, changes or clears how an employee's cut leave days are priced
/// (GSS03 decision 11). Both null on a side means the company divisor applies.
/// </summary>
public sealed record EmployeeLeaveDeductionExceptionChanged(
    Guid EmployeeId,
    decimal? OldFlatAmountPerDay,
    int? OldDivisor,
    decimal? NewFlatAmountPerDay,
    int? NewDivisor)
    : DomainEvent(EmployeeId, nameof(Employee), "employee.leave_deduction_exception_changed");
