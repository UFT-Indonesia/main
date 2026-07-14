using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
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
        AttendanceDayStatus? status)
    {
        ApplyFilters(Query, employeeSearch, dateFrom, dateTo, status);
        Query.Include(day => day.Employee);
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
        AttendanceDayStatus? status)
    {
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
}

internal sealed class AttendanceDayListCountSpec : Specification<AttendanceDay>
{
    public AttendanceDayListCountSpec(
        string? employeeSearch,
        LocalDate? dateFrom,
        LocalDate? dateTo,
        AttendanceDayStatus? status)
    {
        AttendanceDayListSpec.ApplyFilters(Query, employeeSearch, dateFrom, dateTo, status);
        Query.AsNoTracking();
    }
}
