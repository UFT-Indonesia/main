using Ardalis.Specification;
using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Leave.Common;

/// <summary>
/// The employee's still-undecided requests filed within the given instant range — the month
/// the cap in <see cref="LeaveRules.MaxPendingRequestsPerMonth"/> is counted over. Filed date,
/// not leave date: the cap guards a manager's queue against a flood of submissions, and dates
/// spread across future months would sail straight past a start-date count.
/// </summary>
internal sealed class PendingLeaveFiledBetweenSpec : Specification<LeaveRequest>
{
    public PendingLeaveFiledBetweenSpec(EmployeeId employeeId, Instant fromInclusive, Instant toExclusive)
    {
        Query.Where(request => request.EmployeeId == employeeId
                               && request.Status == LeaveRequestStatus.Pending
                               && request.RequestedAtUtc >= fromInclusive
                               && request.RequestedAtUtc < toExclusive);
        Query.AsNoTracking();
    }
}

/// <summary>
/// Approved requests for the employee overlapping the given inclusive range.
/// <paramref name="excludeRequestId"/> drops one request from the result — an edit re-checks its
/// own new dates, and an already-approved request would otherwise be found conflicting with itself.
/// </summary>
internal sealed class ApprovedLeaveOverlappingSpec : Specification<LeaveRequest>
{
    public ApprovedLeaveOverlappingSpec(
        EmployeeId employeeId,
        LocalDate startDate,
        LocalDate endDate,
        LeaveRequestId? excludeRequestId = null)
    {
        Query.Where(request => request.EmployeeId == employeeId
                               && request.Status == LeaveRequestStatus.Approved
                               && request.StartDate <= endDate
                               && startDate <= request.EndDate);

        if (excludeRequestId is { } excluded)
        {
            Query.Where(request => request.Id != excluded);
        }

        Query.AsNoTracking();
    }
}

/// <summary>One request by id, tracked for a lifecycle decision.</summary>
internal sealed class LeaveRequestByIdSpec : SingleResultSpecification<LeaveRequest>
{
    public LeaveRequestByIdSpec(LeaveRequestId id)
    {
        Query.Where(request => request.Id == id);
        Query.Include(request => request.Employee);
    }
}

/// <summary>
/// Approved, full-day leave for the employee covering the given date — a half day or hourly
/// Izin excluded, since the employee is expected to work the rest of that day and must not flip
/// the OnLeave badge on.
/// </summary>
internal sealed class FullDayApprovedLeaveOnDateSpec : Specification<LeaveRequest>
{
    public FullDayApprovedLeaveOnDateSpec(EmployeeId employeeId, LocalDate date)
    {
        Query.Where(request => request.EmployeeId == employeeId
                               && request.Status == LeaveRequestStatus.Approved
                               && !request.HalfDay
                               && request.StartHour == null
                               && request.StartDate <= date
                               && date <= request.EndDate);
        Query.AsNoTracking();
    }
}

/// <summary>
/// Approved requests for a set of employees that OVERLAP the given calendar year span. Overlap
/// rather than "starts in the year", because a request spanning New Year is charged to both
/// years — the days are attributed per year by <see cref="LeaveQuota.WorkdaysInYear"/>, so the
/// year it happened to start in must not decide which quota it eats.
/// </summary>
internal sealed class ApprovedLeaveForYearSpec : Specification<LeaveRequest>
{
    public ApprovedLeaveForYearSpec(IReadOnlyCollection<EmployeeId> employeeIds, int year)
        : this(employeeIds, year, year)
    {
    }

    /// <summary>
    /// <paramref name="excludeRequestId"/> drops one request from the rollup — an edit prices its
    /// own new dates against the quota, and counting the request's existing charge as "already
    /// used" would have it compete with itself for the same days.
    /// </summary>
    public ApprovedLeaveForYearSpec(
        IReadOnlyCollection<EmployeeId> employeeIds,
        int fromYear,
        int toYear,
        LeaveRequestId? excludeRequestId = null)
    {
        var spanStart = new LocalDate(fromYear, 1, 1);
        var spanEnd = new LocalDate(toYear, 12, 31);
        Query.Where(request => employeeIds.Contains(request.EmployeeId)
                               && request.Status == LeaveRequestStatus.Approved
                               && request.StartDate <= spanEnd
                               && spanStart <= request.EndDate);

        if (excludeRequestId is { } excluded)
        {
            Query.Where(request => request.Id != excluded);
        }

        Query.AsNoTracking();
    }
}
