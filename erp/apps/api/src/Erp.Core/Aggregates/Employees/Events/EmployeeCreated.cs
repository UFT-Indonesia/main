using Erp.Core.Aggregates.Common;
using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Employees.Events;

/// <summary>
/// Emitted once when a new <see cref="Employee"/> is created. Carries the full
/// initial snapshot so downstream consumers (HR sync, payroll, webhooks)
/// don't need a follow-up read.
/// </summary>
public sealed record EmployeeCreated(
    Guid EmployeeId,
    string FullName,
    string Nik,
    string? Npwp,
    EmployeeRole Role,
    Guid? ParentId,
    Money MonthlyWage,
    LocalDate EffectiveSalaryFrom)
    : DomainEvent(EmployeeId, nameof(Employee), "employee.created");
