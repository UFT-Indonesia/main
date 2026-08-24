using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Attendance.Common;

/// <summary>All punches for one employee inside a UTC instant window, chronological.</summary>
internal sealed class AttendanceLogsForEmployeeDaySpec : Specification<AttendanceLog>
{
    public AttendanceLogsForEmployeeDaySpec(EmployeeId employeeId, Instant fromInclusive, Instant toExclusive)
    {
        Query.Where(log => log.EmployeeId == employeeId
                           && log.PunchedAtUtc >= fromInclusive
                           && log.PunchedAtUtc < toExclusive);
        Query.Include(log => log.Employee);
        Query.Include(log => log.Notes);
        Query.OrderBy(log => log.PunchedAtUtc);
        Query.AsNoTracking();
    }
}

/// <summary>One punch with its notes loaded, tracked for mutation (add/remove note, edit punch).</summary>
internal sealed class AttendanceLogByIdWithNotesSpec : SingleResultSpecification<AttendanceLog>
{
    public AttendanceLogByIdWithNotesSpec(AttendanceLogId id)
    {
        Query.Where(log => log.Id == id);
        Query.Include(log => log.Notes);
    }
}

/// <summary>
/// The exact punch a device replay would collide with — same employee, device, and instant.
/// Read-only: existence alone is enough to short-circuit a resend as idempotent.
/// </summary>
internal sealed class DevicePunchByKeySpec : SingleResultSpecification<AttendanceLog>
{
    public DevicePunchByKeySpec(EmployeeId employeeId, string deviceId, Instant punchedAtUtc)
    {
        Query.Where(log => log.EmployeeId == employeeId
                           && log.DeviceId == deviceId
                           && log.PunchedAtUtc == punchedAtUtc);
        Query.Include(log => log.Notes);
        Query.AsNoTracking();
    }
}

/// <summary>The materialized day row for one employee + calendar date, tracked for mutation.</summary>
internal sealed class AttendanceDayByEmployeeDateSpec : Specification<AttendanceDay>
{
    public AttendanceDayByEmployeeDateSpec(EmployeeId employeeId, LocalDate calendarDate)
    {
        Query.Where(day => day.EmployeeId == employeeId && day.CalendarDate == calendarDate);
    }
}

/// <summary>
/// The employee's existing rows inside an inclusive date range. Read-only: leave
/// materialization only needs to know which dates are already spoken for.
/// </summary>
internal sealed class AttendanceDayDatesInRangeSpec : Specification<AttendanceDay>
{
    public AttendanceDayDatesInRangeSpec(EmployeeId employeeId, LocalDate startDate, LocalDate endDate)
    {
        Query.Where(day => day.EmployeeId == employeeId
                           && day.CalendarDate >= startDate
                           && day.CalendarDate <= endDate);
        Query.AsNoTracking();
    }
}

/// <summary>Every day a given leave request materialized or covers, tracked for mutation.</summary>
internal sealed class AttendanceDaysForLeaveRequestSpec : Specification<AttendanceDay>
{
    public AttendanceDaysForLeaveRequestSpec(LeaveRequestId leaveRequestId)
    {
        Query.Where(day => day.LeaveRequestId == leaveRequestId);
    }
}

/// <summary>
/// Days that exist only because leave covered them, past the employee's last working day.
/// A null tap-in is what makes a row leave-only: anything with a punch is real attendance
/// and is never swept up by this. Tracked for deletion.
/// </summary>
internal sealed class LeaveOnlyDaysAfterSpec : Specification<AttendanceDay>
{
    public LeaveOnlyDaysAfterSpec(EmployeeId employeeId, LocalDate lastWorkingDay)
    {
        Query.Where(day => day.EmployeeId == employeeId
                           && day.CalendarDate > lastWorkingDay
                           && day.LeaveRequestId != null
                           && day.TapInUtc == null);
    }
}
