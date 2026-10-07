using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Payroll.Common;
using NodaTime;

namespace Erp.UseCases.Leave.Common;

/// <summary>
/// What measuring a request against the quota found. <see cref="Violation"/> is set when it must be
/// refused; otherwise <see cref="OverQuotaDays"/> says how many of its days go past the cap and will be
/// cut from salary (GSS03) — 0 when it fits.
/// </summary>
internal readonly record struct QuotaCheck((string Code, string Message)? Violation, decimal OverQuotaDays)
{
    internal static QuotaCheck Fits => new(null, 0m);
}

/// <summary>
/// The one place a leave request is measured against the employee's quota. Called when the request is
/// filed, for fast feedback, when it is approved, which is the authoritative check, and when it is edited.
/// Running out of Annual, Sick or Izin no longer refuses (the extra days are cut from salary instead);
/// Unpaid stays hard-capped, and a day that would be cut in an already-closed payroll month is refused.
/// </summary>
internal static class LeaveQuotaGuard
{
    internal static async Task<QuotaCheck> CheckAsync(
        Employee employee,
        LeaveType type,
        LocalDate startDate,
        LocalDate endDate,
        bool halfDay,
        int? startHour,
        int? endHour,
        AttendanceDayPolicy policy,
        IRepository<LeaveRequest> leaveRequests,
        IReadRepository<LeaveDeductionMonth> months,
        IReadRepository<LeaveDeductionLine> lines,
        LocalDate today,
        Instant now,
        CancellationToken ct,
        LeaveRequestId? excludeRequestId = null)
    {
        if (employee.Role == EmployeeRole.Owner)
        {
            return QuotaCheck.Fits;
        }

        if (type == LeaveType.Annual && employee.IsOnProbation(today))
        {
            return new(("leave.probation_annual",
                $"{employee.FullName} is on probation until "
                + $"{employee.ProbationEndsOn!.Value:yyyy-MM-dd} and has no annual leave yet."), 0m);
        }

        var chargePerWorkday = LeaveRequest.ChargePerWorkday(halfDay, startHour, endHour, policy);

        if (type == LeaveType.Unpaid && await UnpaidCapViolationAsync(
                employee, startDate, endDate, chargePerWorkday, policy, leaveRequests, today, ct, excludeRequestId) is { } capped)
        {
            return new(capped, 0m);
        }

        var days = await LeaveDeductionEngine.CandidateDaysAsync(
            employee, type, startDate, endDate, chargePerWorkday, now, policy, today, leaveRequests, lines, ct,
            excludeRequestId);
        var over = days.Sum(d => d.CutDays);

        if (over > 0)
        {
            var closed = await LeaveDeductionEngine.ClosedMonthsAsync(months, ct);
            if (days.Any(d => d.CutDays > 0 && closed.Contains(LeaveDeductionMonth.MonthOf(d.Date))))
            {
                return new(PayrollClosed, over);
            }
        }

        return new(null, over);
    }

    internal static readonly (string Code, string Message) PayrollClosed = ("leave.payroll_closed",
        "This leave has days in a month whose payroll is already closed and would cost salary. It can't be added.");

    /// <summary>Unpaid is the one type still refused for running out: it is cut in full, so the cap is a hard limit.</summary>
    private static async Task<(string Code, string Message)?> UnpaidCapViolationAsync(
        Employee employee,
        LocalDate startDate,
        LocalDate endDate,
        decimal chargePerWorkday,
        AttendanceDayPolicy policy,
        IRepository<LeaveRequest> leaveRequests,
        LocalDate today,
        CancellationToken ct,
        LeaveRequestId? excludeRequestId)
    {
        // Days are charged to the year they fall in, so a request across New Year has to fit
        // both years' remaining quota — neither year subsidises the other.
        var requestedByYear = LeaveRequest.Workdays(startDate, endDate, policy)
            .GroupBy(date => date.Year)
            .ToDictionary(group => group.Key, group => group.Count() * chargePerWorkday);

        var capped = requestedByYear.Keys
            .Select(year => (Year: year, Entitled: LeaveQuota.Entitled(LeaveType.Unpaid, employee, year, today)))
            .Where(entry => entry.Entitled.HasValue)
            .ToList();

        if (capped.Count == 0)
        {
            return null;
        }

        var approved = await leaveRequests.ListAsync(
            new ApprovedLeaveForYearSpec(
                [employee.Id], capped.Min(e => e.Year), capped.Max(e => e.Year), excludeRequestId),
            ct);

        foreach (var (year, entitled) in capped)
        {
            // Pending requests do not reserve days: only an approval actually spends quota, so a
            // request that sits unapproved never blocks the next one.
            var remaining = entitled!.Value - LeaveQuota.UsedDays(approved, LeaveType.Unpaid, year, policy);
            var requested = requestedByYear[year];
            if (requested <= remaining)
            {
                continue;
            }

            return ("leave.quota_exceeded",
                $"Only {Math.Max(remaining, 0)} {LeaveType.Unpaid} day(s) remain for {year}; "
                + $"this request uses {requested}.");
        }

        return null;
    }
}
