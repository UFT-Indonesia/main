using Ardalis.Specification;
using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Leave.Common;

/// <summary>The employee's open request, if any — at most one Pending is allowed at a time.</summary>
internal sealed class PendingLeaveForEmployeeSpec : Specification<LeaveRequest>
{
    public PendingLeaveForEmployeeSpec(EmployeeId employeeId)
    {
        Query.Where(request => request.EmployeeId == employeeId
                               && request.Status == LeaveRequestStatus.Pending);
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
/// Approved requests STARTING in the given calendar year for a set of employees — used to
/// compute the "approved workdays this year" counter shown to approvers.
/// ponytail: a request spanning New Year is attributed entirely to its start year; split
/// per-year attribution if that ever misleads.
/// </summary>
internal sealed class ApprovedLeaveForYearSpec : Specification<LeaveRequest>
{
    public ApprovedLeaveForYearSpec(IReadOnlyCollection<EmployeeId> employeeIds, int year)
    {
        var yearStart = new LocalDate(year, 1, 1);
        var yearEnd = new LocalDate(year, 12, 31);
        Query.Where(request => employeeIds.Contains(request.EmployeeId)
                               && request.Status == LeaveRequestStatus.Approved
                               && request.StartDate >= yearStart
                               && request.StartDate <= yearEnd);
        Query.AsNoTracking();
    }
}
