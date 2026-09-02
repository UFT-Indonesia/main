using Erp.Core.Aggregates.Leave;
using Erp.UseCases.Common;

namespace Erp.UseCases.Leave.EditLeaveRequest;

/// <summary>
/// Moves an existing request's dates and half-day/hourly shape. Type, reason and attachment are
/// absent on purpose — changing those makes it a different absence, which is what cancel-and-refile
/// is for. Every field is a full replacement, exactly as it would be on a new request.
/// </summary>
public sealed record EditLeaveRequestCommand(
    Guid LeaveRequestId,
    DateOnly StartDate,
    DateOnly EndDate,
    bool HalfDay,
    HalfDayPeriod? HalfDayPeriod,
    int? StartHour,
    int? EndHour,
    Caller Caller);
