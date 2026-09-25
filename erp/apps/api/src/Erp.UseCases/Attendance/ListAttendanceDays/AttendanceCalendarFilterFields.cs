using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Attendance.ListAttendanceDays;

/// <summary>
/// What the Kehadiran filter builder may ask about. Declared over <see cref="Employee"/>, not
/// over AttendanceDay: a row is now a calendar date, so a filter narrows *which employees are
/// counted on every date*, never which dates exist.
/// <para>
/// Only identity fields are offered. Filtering by status or tap time would silently redefine
/// the per-date counts to describe a subset while they still read like totals — the
/// problems-only toggle covers that need instead. Which employees a caller may see at all is
/// decided by ApplyCallerScope on the spec; these filters only narrow that further.
/// </para>
/// </summary>
public static class AttendanceCalendarFilterFields
{
    public static readonly FilterFieldMap<Employee> Fields = new FilterFieldMap<Employee>()
        .Text("employeeName", employee => employee.FullName)
        .Relation("employeeId", employee => employee.Id, id => new EmployeeId(id));
}
