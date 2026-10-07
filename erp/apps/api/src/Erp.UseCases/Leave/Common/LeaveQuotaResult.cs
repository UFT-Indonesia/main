using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using NodaTime;

namespace Erp.UseCases.Leave.Common;

/// <summary>
/// One leave type's standing for one employee in one year. Null entitled/remaining means
/// uncapped — an Owner, or a type with no override.
/// </summary>
public sealed class LeaveQuotaResult
{
    public string Type { get; init; } = default!;
    public decimal? EntitledDays { get; init; }
    public decimal UsedDays { get; init; }

    /// <summary>
    /// May be negative, for an employee who was already over when a cap was set. Reported raw
    /// rather than clamped to zero — an Owner setting a cap should see the overage they created.
    /// </summary>
    public decimal? RemainingDays { get; init; }

    /// <summary>
    /// Days of this type that went past the cap in the year and cost salary (GSS03). Zero when
    /// nothing is cut. <see cref="UsedDays"/> still counts them; <see cref="RemainingDays"/> does not.
    /// </summary>
    public decimal OverQuotaDays { get; init; }

    /// <summary>
    /// <paramref name="allocation"/> is the employee's approved leave split into free and cut days
    /// (see <c>LeaveDeductionEngine</c>); without it every used day counts as free.
    /// </summary>
    public static LeaveQuotaResult For(
        Employee employee,
        LeaveType type,
        int year,
        LocalDate today,
        IEnumerable<LeaveRequest> approvedOverlappingYear,
        AttendanceDayPolicy policy,
        IReadOnlyList<LeaveDeductionDay>? allocation = null)
    {
        var entitled = LeaveQuota.Entitled(type, employee, year, today);
        var used = LeaveQuota.UsedDays(approvedOverlappingYear, type, year, policy);

        // Unpaid is cut in full and hard-capped on what is taken, so it still counts every day;
        // the other types only spend their cap on free days.
        var ofType = allocation?.Where(d => d.Type == type && d.Date.Year == year).ToList();
        var spent = type == LeaveType.Unpaid || ofType is null ? used : ofType.Sum(d => d.FreeDays);

        return new LeaveQuotaResult
        {
            Type = type.ToString(),
            EntitledDays = entitled,
            UsedDays = used,
            RemainingDays = entitled - spent,
            OverQuotaDays = type == LeaveType.Unpaid ? 0m : ofType?.Sum(d => d.CutDays) ?? 0m,
        };
    }
}
