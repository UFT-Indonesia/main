using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Attendance.Holidays.Common;

namespace Erp.UseCases.Attendance.Holidays;

/// <summary>
/// Brings leave in line with a date that just became, or stopped being, a holiday. Quota needs
/// nothing — it is recounted through the policy on every read — but the stored
/// <see cref="LeaveRequest.WorkdayCount"/> and the one attendance row an approved leave
/// materializes on its first workday both have to follow.
/// <para>
/// Runs as its own message after the holiday change committed, so the injected policy was
/// loaded with the calendar as it now stands.
/// </para>
/// </summary>
public static class HolidayCalendarChangedHandler
{
    public static async Task Handle(
        HolidayCalendarChanged message,
        IRepository<LeaveRequest> leaveRequests,
        IRepository<AttendanceDay> attendanceDays,
        AttendanceDayPolicy policy,
        CancellationToken ct)
    {
        var affected = await leaveRequests.ListAsync(new LiveLeaveCoveringDateSpec(message.Date), ct);

        foreach (var request in affected)
        {
            if (request.RecountWorkdays(policy))
            {
                await leaveRequests.UpdateAsync(request, ct);
            }

            if (request.Status == LeaveRequestStatus.Approved && !request.HalfDay && request.StartHour is null)
            {
                await LeaveAttendanceSync.ReanchorAsync(request, policy, attendanceDays, ct);
            }
        }
    }
}
