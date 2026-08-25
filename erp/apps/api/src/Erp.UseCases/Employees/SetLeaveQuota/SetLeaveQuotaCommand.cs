using Erp.UseCases.Common;

namespace Erp.UseCases.Employees.SetLeaveQuota;

/// <summary>Null <paramref name="Days"/> clears the override for that type.</summary>
public sealed record SetLeaveQuotaCommand(Guid EmployeeId, string Type, int? Days, Caller Caller);
