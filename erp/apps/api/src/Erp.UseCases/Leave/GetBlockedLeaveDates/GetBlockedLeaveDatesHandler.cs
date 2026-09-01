using Ardalis.Specification;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Leave.GetBlockedLeaveDates;

/// <summary>
/// Days a date picker must not let anyone select for this employee, as raw approved ranges.
///
/// Deliberately not <c>ListLeaveRequests</c>: that endpoint caps at 100 rows ordered by
/// request date descending, so an employee's oldest leave silently falls off the page after a
/// few years and quietly stops being blocked. It also runs a count and a yearly balance rollup
/// for data thrown away here. This is one indexed read over (employee_id, status).
///
/// Ranges are returned exactly as stored, weekends included. A Fri–Mon leave blocks the whole
/// span even though only the workdays materialize attendance rows — the person is away either
/// way, and expanding to workdays here would duplicate <see cref="LeaveRequest.Workdays"/>.
///
/// Approved only, mirroring <c>ApprovedLeaveOverlappingSpec</c>: that is the rule the server
/// actually enforces on create, so the greyed days and a rejection describe the same set. A
/// pending request is not leave yet.
/// </summary>
public static class GetBlockedLeaveDatesHandler
{
    public static async Task<Result<BlockedLeaveDatesResult>> Handle(
        GetBlockedLeaveDatesQuery query,
        IReadRepository<LeaveRequest> leaveRequests,
        CancellationToken ct)
    {
        if (query.From > query.To)
        {
            return new Result<BlockedLeaveDatesResult>.Error(
                "leave.date_range", "From must be on or before To.");
        }

        // No authority filter, matching ListLeaveRequests: the leave calendar is open to every
        // colleague. This returns strictly less than that endpoint already does — dates only,
        // no type, no reason.
        var ranges = await leaveRequests.ListAsync(
            new ApprovedLeaveOverlappingWindowSpec(
                new EmployeeId(query.EmployeeId),
                LocalDate.FromDateOnly(query.From),
                LocalDate.FromDateOnly(query.To)),
            ct);

        return new Result<BlockedLeaveDatesResult>.Success(new BlockedLeaveDatesResult
        {
            Ranges = [.. ranges.Select(r => new BlockedLeaveRange(
                r.StartDate.ToDateOnly(), r.EndDate.ToDateOnly()))],
        });
    }
}

/// <summary>Approved leave for one employee overlapping an inclusive window.</summary>
internal sealed class ApprovedLeaveOverlappingWindowSpec : Specification<LeaveRequest>
{
    public ApprovedLeaveOverlappingWindowSpec(EmployeeId employeeId, LocalDate from, LocalDate to)
    {
        Query.Where(request => request.EmployeeId == employeeId
                               && request.Status == LeaveRequestStatus.Approved
                               && request.StartDate <= to
                               && request.EndDate >= from);
        Query.OrderBy(request => request.StartDate);
        Query.AsNoTracking();
    }
}
