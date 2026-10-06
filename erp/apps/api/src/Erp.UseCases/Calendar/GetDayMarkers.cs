using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Overtime.Common;
using NodaTime;

namespace Erp.UseCases.Calendar;

/// <summary>What a date picker marks on one employee's dates: leave, OT and nothing about why.</summary>
public static class DayMarkerKinds
{
    public const string Leave = "Leave";
    public const string LeavePending = "LeavePending";
    public const string Overtime = "Overtime";
    public const string OvertimePending = "OvertimePending";
}

public sealed record DayMarker(DateOnly Date, string Kind);

public sealed record DayMarkersResult(IReadOnlyList<DayMarker> Markers);

/// <summary>The caller is unused: like the leave calendar, anyone signed in may see that a colleague is away or on OT.</summary>
public sealed record GetDayMarkersQuery(Guid EmployeeId, DateOnly From, DateOnly To, Caller Caller);

/// <summary>
/// Dates in a window that carry leave (Pending or Approved) or an OT assignment (Pending, or
/// Approved/Expired) for one employee, so every date picker can mark them the same way. Only
/// the kind is returned — never type, reason or hours: that is the point of "generic".
/// </summary>
public static class GetDayMarkersHandler
{
    /// <summary>Keeps the per-date loop bounded; From/To are raw client input.</summary>
    private const int MaxWindowDays = 1100;

    public static async Task<Result<DayMarkersResult>> Handle(
        GetDayMarkersQuery query,
        IReadRepository<LeaveRequest> leaveRequests,
        IReadRepository<OvertimeAssignment> overtime,
        AttendanceDayPolicy policy,
        CancellationToken ct)
    {
        if (query.From > query.To)
        {
            return new Result<DayMarkersResult>.Error("calendar.date_range", "From must be on or before To.");
        }

        if (query.To.DayNumber - query.From.DayNumber > MaxWindowDays)
        {
            return new Result<DayMarkersResult>.Error(
                "calendar.date_range_too_wide", $"The window cannot exceed {MaxWindowDays} days.");
        }

        var employeeId = new EmployeeId(query.EmployeeId);
        var from = LocalDate.FromDateOnly(query.From);
        var to = LocalDate.FromDateOnly(query.To);
        var markers = new HashSet<(LocalDate Date, string Kind)>();

        foreach (var leave in await leaveRequests.ListAsync(new LiveLeaveInWindowSpec(employeeId, from, to), ct))
        {
            var kind = leave.Status == LeaveRequestStatus.Approved ? DayMarkerKinds.Leave : DayMarkerKinds.LeavePending;
            foreach (var date in LeaveRequest.Workdays(
                         leave.StartDate > from ? leave.StartDate : from, leave.EndDate < to ? leave.EndDate : to, policy))
            {
                markers.Add((date, kind));
            }
        }

        foreach (var assignment in await overtime.ListAsync(new OvertimeOwningPunchesSpec(employeeId, from, to), ct))
        {
            markers.Add((
                assignment.Date,
                assignment.Status == OvertimeStatus.Pending ? DayMarkerKinds.OvertimePending : DayMarkerKinds.Overtime));
        }

        return new Result<DayMarkersResult>.Success(new DayMarkersResult(
            markers.OrderBy(m => m.Date).ThenBy(m => m.Kind).Select(m => new DayMarker(m.Date.ToDateOnly(), m.Kind)).ToList()));
    }
}

internal sealed class LiveLeaveInWindowSpec : Specification<LeaveRequest>
{
    public LiveLeaveInWindowSpec(EmployeeId employeeId, LocalDate from, LocalDate to)
    {
        Query.Where(r => r.EmployeeId == employeeId
                         && (r.Status == LeaveRequestStatus.Pending || r.Status == LeaveRequestStatus.Approved)
                         && r.StartDate <= to && from <= r.EndDate);
        Query.AsNoTracking();
    }
}
