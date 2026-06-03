using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;

namespace Erp.UseCases.Attendance.ListAttendanceLogs;

internal sealed class EmployeeNameSearchSpec : Specification<Employee>
{
    public EmployeeNameSearchSpec(string search)
    {
        var needle = search.Trim().ToLowerInvariant();
        Query.Where(e => e.FullName.ToLower().Contains(needle));
        Query.AsNoTracking();
    }
}
