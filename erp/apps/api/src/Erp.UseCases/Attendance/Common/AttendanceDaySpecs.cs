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
        Query.OrderBy(log => log.PunchedAtUtc);
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
