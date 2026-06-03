using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;

namespace Erp.UseCases.Attendance.ListAttendanceLogs;

internal sealed class EmployeeIdBatchSpec : Specification<Employee>
{
    public EmployeeIdBatchSpec(IReadOnlyList<Guid> ids)
    {
        Query.Where(e => ids.Contains(e.Id.Value));
        Query.AsNoTracking();
    }
}
