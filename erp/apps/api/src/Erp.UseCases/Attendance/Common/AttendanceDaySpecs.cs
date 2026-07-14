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

/// <summary>The materialized day row for one employee + calendar date, tracked for mutation.</summary>
internal sealed class AttendanceDayByEmployeeDateSpec : Specification<AttendanceDay>
{
    public AttendanceDayByEmployeeDateSpec(EmployeeId employeeId, LocalDate calendarDate)
    {
        Query.Where(day => day.EmployeeId == employeeId && day.CalendarDate == calendarDate);
    }
}
