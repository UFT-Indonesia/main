using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Leave.GetBlockedLeaveDates;

/// <summary>
/// Which dates a date picker must not let this employee land on, split into two severities:
/// dates that genuinely conflict with the request being built (unselectable) and dates that
/// carry an approved leave which does not conflict with it (selectable, but worth a visual
/// hint — some of that day is already spoken for).
///
/// Deliberately not <c>ListLeaveRequests</c>: that endpoint caps at 100 rows ordered by
/// request date descending, so an employee's oldest leave silently falls off the page after a
/// few years and quietly stops being blocked. It also runs a count and a yearly balance rollup
/// for data thrown away here. This is one indexed read over (employee_id, status).
///
/// Approved only, mirroring <c>ApprovedLeaveOverlappingSpec</c>: that is the rule the server
/// actually enforces on create, so what the picker shows and a rejection describe the same set.
/// A pending request is not leave yet.
/// </summary>
public static class GetBlockedLeaveDatesHandler
{
    /// <summary>
    /// Comfortably above the widest window any caller actually needs (useBlockedLeaveDates asks
    /// for last-year..next-year, ~3 years) — this exists only to keep the per-date loop below
    /// bounded, since From/To arrive as raw client input with no other size limit.
    /// </summary>
    private const int MaxWindowDays = 1100;

    public static async Task<Result<BlockedLeaveDatesResult>> Handle(
        GetBlockedLeaveDatesQuery query,
        IReadRepository<LeaveRequest> leaveRequests,
        AttendanceDayPolicy policy,
        CancellationToken ct)
    {
        if (query.From > query.To)
        {
            return new Result<BlockedLeaveDatesResult>.Error(
                "leave.date_range", "From must be on or before To.");
        }

        if (query.To.DayNumber - query.From.DayNumber > MaxWindowDays)
        {
            return new Result<BlockedLeaveDatesResult>.Error(
                "leave.date_range_too_wide", $"The window cannot exceed {MaxWindowDays} days.");
        }

        // No authority filter, matching ListLeaveRequests: the leave calendar is open to every
        // colleague. This returns strictly less than that endpoint already does — dates only,
        // no type, no reason.
        var from = LocalDate.FromDateOnly(query.From);
        var to = LocalDate.FromDateOnly(query.To);
        var approved = await leaveRequests.ListAsync(
            new ApprovedLeaveOverlappingWindowSpec(new EmployeeId(query.EmployeeId), from, to), ct);

        if (approved.Count == 0)
        {
            return new Result<BlockedLeaveDatesResult>.Success(new BlockedLeaveDatesResult());
        }

        // Nothing chosen yet (type/half/hour still blank in the form) is treated as a full-day
        // request — the conservative default, since a full day is the only shape that conflicts
        // with everything a narrower one would too.
        var candidateWindow = LeaveRequest.OccupiedWindow(
            query.HalfDay, query.HalfDayPeriod, query.StartHour, query.EndHour, policy);

        var blocked = new List<DateOnly>();
        var partial = new List<DateOnly>();

        for (var date = from; date <= to; date = date.PlusDays(1))
        {
            var onThisDate = approved.Where(r => r.StartDate <= date && date <= r.EndDate).ToList();
            if (onThisDate.Count == 0)
            {
                continue;
            }

            var conflicts = onThisDate.Any(r =>
                LeaveRequest.WindowsIntersect(r.OccupiedWindow(policy), candidateWindow));
            (conflicts ? blocked : partial).Add(date.ToDateOnly());
        }

        return new Result<BlockedLeaveDatesResult>.Success(new BlockedLeaveDatesResult
        {
            BlockedDates = blocked,
            PartialDates = partial,
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
