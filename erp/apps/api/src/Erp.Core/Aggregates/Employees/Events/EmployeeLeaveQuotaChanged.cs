using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Domain;

namespace Erp.Core.Aggregates.Employees.Events;

/// <summary>
/// Emitted when a per-employee leave quota override is set, changed or cleared. Null days means
/// no override — the default applies again.
/// </summary>
public sealed record EmployeeLeaveQuotaChanged(
    Guid EmployeeId,
    LeaveType Type,
    int? OldEntitledDays,
    int? NewEntitledDays)
    : DomainEvent(EmployeeId, nameof(Employee), "employee.leave_quota_changed");
