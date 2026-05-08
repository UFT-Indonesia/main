using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Employees.Events;

/// <summary>
/// Emitted once when an employee is terminated. <see cref="TerminationDate"/>
/// is the business-fact effective date (e.g. last working day) — distinct from
/// the event envelope's <c>RaisedAt</c>.
/// </summary>
public sealed record EmployeeTerminated(
    Guid EmployeeId,
    LocalDate TerminationDate)
    : DomainEvent(EmployeeId, nameof(Employee), "employee.terminated");
