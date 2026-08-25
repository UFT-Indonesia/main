using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using NodaTime;

namespace Erp.UseCases.Leave.Common;

/// <summary>
/// One leave type's standing for one employee in one year. Null entitled/remaining means
/// uncapped — an Owner, or a type with no override.
/// </summary>
public sealed class LeaveQuotaResult
{
    public string Type { get; init; } = default!;
    public int? EntitledDays { get; init; }
    public int UsedDays { get; init; }

    /// <summary>
    /// May be negative, for an employee who was already over when a cap was set. Reported raw
    /// rather than clamped to zero — an Owner setting a cap should see the overage they created.
    /// </summary>
    public int? RemainingDays { get; init; }

    public static LeaveQuotaResult For(
        Employee employee,
        LeaveType type,
        int year,
        LocalDate today,
        IEnumerable<LeaveRequest> approvedOverlappingYear)
    {
        var entitled = LeaveQuota.Entitled(type, employee, year, today);
        var used = LeaveQuota.UsedDays(approvedOverlappingYear, type, year);

        return new LeaveQuotaResult
        {
            Type = type.ToString(),
            EntitledDays = entitled,
            UsedDays = used,
            RemainingDays = entitled - used,
        };
    }
}
