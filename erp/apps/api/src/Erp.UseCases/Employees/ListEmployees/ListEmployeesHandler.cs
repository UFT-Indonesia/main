using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common.Filtering;
using Erp.UseCases.Employees.Common;

namespace Erp.UseCases.Employees.ListEmployees;

public static class ListEmployeesHandler
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static async Task<Result<ListEmployeesResult>> Handle(
        ListEmployeesQuery query,
        IReadRepository<Employee> employees,
        CancellationToken ct)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(query.PageSize, MaxPageSize);

        if (!FilterApplier.TryCompile(EmployeeFilterFields.Fields, query.Filters, query.Caller, out var filters, out var failure))
        {
            return new Result<ListEmployeesResult>.Error(failure.Code, failure.Message);
        }

        var totalCount = await employees.CountAsync(new EmployeeListCountSpec(filters), ct);
        var items = await employees.ListAsync(new EmployeeListSpec(page, pageSize, filters), ct);

        return new Result<ListEmployeesResult>.Success(new ListEmployeesResult
        {
            Items = items.Select(EmployeeMapper.ToResult).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        });
    }
}
