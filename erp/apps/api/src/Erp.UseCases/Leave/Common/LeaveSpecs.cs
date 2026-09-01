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

/// <summary>Approved requests for the employee overlapping the given inclusive range.</summary>
internal sealed class ApprovedLeaveOverlappingSpec : Specification<LeaveRequest>
{
    public ApprovedLeaveOverlappingSpec(EmployeeId employeeId, LocalDate startDate, LocalDate endDate)
    {
        Query.Where(request => request.EmployeeId == employeeId
                               && request.Status == LeaveRequestStatus.Approved
                               && request.StartDate <= endDate
                               && startDate <= request.EndDate);
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

    public ApprovedLeaveForYearSpec(IReadOnlyCollection<EmployeeId> employeeIds, int fromYear, int toYear)
    {
        var spanStart = new LocalDate(fromYear, 1, 1);
        var spanEnd = new LocalDate(toYear, 12, 31);
        Query.Where(request => employeeIds.Contains(request.EmployeeId)
                               && request.Status == LeaveRequestStatus.Approved
                               && request.StartDate <= spanEnd
                               && spanStart <= request.EndDate);
        Query.AsNoTracking();
    }
}
