using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Leave.Common;

/// <summary>
/// The one place a leave request is measured against the employee's quota. Called twice — once
/// when the request is filed, for fast feedback, and again when it is approved, which is the
/// authoritative check: a quota lowered while the request sat pending must not be approvable past.
/// </summary>
internal static class LeaveQuotaGuard
{
    /// <summary>Null when the request fits; otherwise the error code and message to return.</summary>
    internal static async Task<(string Code, string Message)?> CheckAsync(
        Employee employee,
        LeaveType type,
        LocalDate startDate,
        LocalDate endDate,
        bool halfDay,
        int? startHour,
        int? endHour,
        AttendanceDayPolicy policy,
        IRepository<LeaveRequest> leaveRequests,
        LocalDate today,
        CancellationToken ct,
        LeaveRequestId? excludeRequestId = null)
    {
        if (employee.Role == EmployeeRole.Owner)
        {
            return null;
        }

        if (type == LeaveType.Annual && employee.IsOnProbation(today))
        {
            return ("leave.probation_annual",
                $"{employee.FullName} is on probation until "
                + $"{employee.ProbationEndsOn!.Value:yyyy-MM-dd} and has no annual leave yet.");
        }

        var chargePerWorkday = LeaveRequest.ChargePerWorkday(halfDay, startHour, endHour, policy);

        // Days are charged to the year they fall in, so a request across New Year has to fit
        // both years' remaining quota — neither year subsidises the other.
        var requestedByYear = LeaveRequest.Workdays(startDate, endDate)
            .GroupBy(date => date.Year)
            .ToDictionary(group => group.Key, group => group.Count() * chargePerWorkday);

        var capped = requestedByYear.Keys
            .Select(year => (Year: year, Entitled: LeaveQuota.Entitled(type, employee, year, today)))
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
            var remaining = entitled!.Value - LeaveQuota.UsedDays(approved, type, year, policy);
            var requested = requestedByYear[year];
            if (requested <= remaining)
            {
                continue;
            }

            return ("leave.quota_exceeded",
                $"Only {Math.Max(remaining, 0)} {type} day(s) remain for {year}; "
                + $"this request uses {requested}.");
        }

        return null;
    }
}
