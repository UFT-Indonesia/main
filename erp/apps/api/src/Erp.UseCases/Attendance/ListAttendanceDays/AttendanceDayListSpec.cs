using System.Linq.Expressions;
using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Attendance.ListAttendanceDays;

internal sealed class AttendanceDayListSpec : Specification<AttendanceDay>
{
    public AttendanceDayListSpec(
        int page,
        int pageSize,
        IReadOnlyList<Expression<Func<AttendanceDay, bool>>> filters,
        Caller caller)
    {
        ApplyFilters(Query, filters, caller);
        Query.Include(day => day.Employee);
        Query.Include(day => day.LeaveRequest);
        Query.OrderByDescending(day => day.CalendarDate)
            .ThenBy(day => day.Employee!.FullName);
        Query.AsNoTracking();
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }

    internal static void ApplyFilters(
        ISpecificationBuilder<AttendanceDay> query,
        IReadOnlyList<Expression<Func<AttendanceDay, bool>>> filters,
        Caller caller)
    {
        // Caller scope first, always: a filter row narrows what this caller may already see and
        // must never be able to widen it.
        ApplyCallerScope(query, caller);
        FilterApplier.ApplyTo(query, filters);
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
        IReadOnlyList<Expression<Func<AttendanceDay, bool>>> filters,
        Caller caller)
    {
        AttendanceDayListSpec.ApplyFilters(Query, filters, caller);
        Query.AsNoTracking();
    }
}
