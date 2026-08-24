using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Common;
using NodaTime;

namespace Erp.UseCases.Attendance.ListAttendanceDays;

internal sealed class AttendanceDayListSpec : Specification<AttendanceDay>
{
    public AttendanceDayListSpec(
        int page,
        int pageSize,
        string? employeeSearch,
        LocalDate? dateFrom,
        LocalDate? dateTo,
        AttendanceDayStatus? status,
        Caller caller)
    {
        ApplyFilters(Query, employeeSearch, dateFrom, dateTo, status, caller);
        Query.Include(day => day.Employee);
        Query.Include(day => day.LeaveRequest);
        Query.OrderByDescending(day => day.CalendarDate)
            .ThenBy(day => day.Employee!.FullName);
        Query.AsNoTracking();
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }

    internal static void ApplyFilters(
        ISpecificationBuilder<AttendanceDay> query,
        string? employeeSearch,
        LocalDate? dateFrom,
        LocalDate? dateTo,
        AttendanceDayStatus? status,
        Caller caller)
    {
        ApplyCallerScope(query, caller);

        if (!string.IsNullOrWhiteSpace(employeeSearch))
        {
            var needle = employeeSearch.Trim().ToLowerInvariant();
            query.Where(day => day.Employee!.FullName.ToLower().Contains(needle));
        }

        if (dateFrom.HasValue)
        {
            query.Where(day => day.CalendarDate >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query.Where(day => day.CalendarDate <= dateTo.Value);
        }

        if (status.HasValue)
        {
            query.Where(day => day.Status == status.Value);
        }
    }

    /// <summary>Staff never see anyone else's days; Owner and Manager see the whole company.</summary>
    private static void ApplyCallerScope(ISpecificationBuilder<AttendanceDay> query, Caller caller)
    {
        if (AttendanceRules.CanReadAll(caller))
        {
            return;
        }

        if (caller.EmployeeId is not { } callerEmployeeId)
        {
            query.Where(_ => false);
            return;
        }

        query.Where(day => day.EmployeeId == callerEmployeeId);
    }
}

internal sealed class AttendanceDayListCountSpec : Specification<AttendanceDay>
{
    public AttendanceDayListCountSpec(
        string? employeeSearch,
        LocalDate? dateFrom,
        LocalDate? dateTo,
        AttendanceDayStatus? status,
        Caller caller)
    {
        AttendanceDayListSpec.ApplyFilters(Query, employeeSearch, dateFrom, dateTo, status, caller);
        Query.AsNoTracking();
    }
}
