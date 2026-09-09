using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Employees.ListEmployees;

public sealed record ListEmployeesQuery(
    int Page,
    int PageSize,
    IReadOnlyList<FilterRow> Filters,
    Caller Caller);
