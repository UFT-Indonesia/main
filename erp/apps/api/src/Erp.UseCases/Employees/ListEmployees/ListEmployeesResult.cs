using Erp.UseCases.Employees.Common;

namespace Erp.UseCases.Employees.ListEmployees;

public sealed class ListEmployeesResult
{
    public IReadOnlyList<EmployeeResult> Items { get; init; } = Array.Empty<EmployeeResult>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
