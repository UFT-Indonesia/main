using Erp.UseCases.Common;

namespace Erp.UseCases.Leave.GetBlockedLeaveDates;

/// <summary>
/// The approved leave ranges for one employee that overlap [From, To]. Bounded on purpose:
/// the window is what keeps this from growing with tenure the way a paged list would.
/// </summary>
public sealed record GetBlockedLeaveDatesQuery(Guid EmployeeId, DateOnly From, DateOnly To, Caller Caller);

public sealed record BlockedLeaveRange(DateOnly StartDate, DateOnly EndDate);

public sealed class BlockedLeaveDatesResult
{
    public IReadOnlyList<BlockedLeaveRange> Ranges { get; init; } = [];
}
