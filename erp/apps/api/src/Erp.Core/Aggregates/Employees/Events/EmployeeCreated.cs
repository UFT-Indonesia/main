using Erp.SharedKernel.Domain;

namespace Erp.Core.Aggregates.Employees.Events;

public sealed record EmployeeCreated(Guid EmployeeId, string Nik, EmployeeRole Role) : DomainEvent;
