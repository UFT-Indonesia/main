using Erp.Core.Aggregates.Common;
using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Employees.Events;

public sealed record EmployeeSalaryChanged(
    Guid EmployeeId,
    Money NewMonthlyWage,
    LocalDate EffectiveFrom) : DomainEvent;
