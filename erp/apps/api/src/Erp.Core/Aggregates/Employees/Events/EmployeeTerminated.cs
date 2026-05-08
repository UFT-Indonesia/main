using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Employees.Events;

public sealed record EmployeeTerminated(Guid EmployeeId, LocalDate TerminationDate) : DomainEvent;
