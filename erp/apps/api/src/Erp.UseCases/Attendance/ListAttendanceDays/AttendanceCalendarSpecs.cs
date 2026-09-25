using System.Linq.Expressions;
using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;
using NodaTime;

namespace Erp.UseCases.Attendance.ListAttendanceDays;

/// <summary>
/// The population the calendar counts against: everyone the caller may see who was employed at
/// some point in the period, narrowed by the filter rows. Only the coarse overlap is checked here;
/// the exact per-date check stays in the handler — one employee can be employed for part of the
/// period and not the rest.
/// </summary>
internal sealed class AttendanceCalendarEmployeesSpec : Specification<Employee>
{
    public AttendanceCalendarEmployeesSpec(
        LocalDate from,
        LocalDate to,
        IReadOnlyList<Expression<Func<Employee, bool>>> filters,
        Caller caller)
    {
        // Caller scope first, always: a filter row narrows what this caller may already see and
        // must never be able to widen it.
        ApplyCallerScope(Query, caller);
        FilterApplier.ApplyTo(Query, filters);

        // Same bounds as the per-date check in AttendanceCalendar: hired on or before, left after.
        Query.Where(employee => (employee.HireDate == null || employee.HireDate <= to)
            && (employee.TerminationDate == null || employee.TerminationDate > from));
        Query.OrderBy(employee => employee.FullName);
        Query.AsNoTracking();
    }

    /// <summary>Staff never see anyone else; Owner and Manager see the whole company.</summary>
    private static void ApplyCallerScope(ISpecificationBuilder<Employee> query, Caller caller)
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

        query.Where(employee => employee.Id == callerEmployeeId);
    }
}

/// <summary>
/// Materialized days in the period for the given employees — the already caller-scoped set
/// from the spec above, so the query never reads rows the caller may not see.
/// </summary>
internal sealed class AttendanceDaysInRangeSpec : Specification<AttendanceDay>
{
    public AttendanceDaysInRangeSpec(LocalDate from, LocalDate to, IReadOnlyCollection<EmployeeId> employeeIds)
    {
        Query.Where(day => day.CalendarDate >= from && day.CalendarDate <= to
            && employeeIds.Contains(day.EmployeeId));
        Query.Include(day => day.LeaveRequest);
        Query.AsNoTracking();
    }
}
