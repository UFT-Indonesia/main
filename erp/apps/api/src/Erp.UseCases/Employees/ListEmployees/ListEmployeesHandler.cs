using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.Common;

namespace Erp.UseCases.Employees.ListEmployees;

public sealed class ListEmployeesHandler
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IReadRepository<Employee> _employees;

    public ListEmployeesHandler(IReadRepository<Employee> employees)
    {
        _employees = employees;
    }

    public async Task<Result<ListEmployeesResult>> Handle(
        ListEmployeesQuery query,
        CancellationToken ct)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(query.PageSize, MaxPageSize);

        EmployeeRole? roleFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            if (!Enum.TryParse<EmployeeRole>(query.Role, ignoreCase: true, out var parsedRole))
            {
                return new Result<ListEmployeesResult>.Error(
                    "employee.role_invalid",
                    "Role must be Owner, Manager, or Staff.");
            }

            roleFilter = parsedRole;
        }

        EmployeeStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<EmployeeStatus>(query.Status, ignoreCase: true, out var parsedStatus))
            {
                return new Result<ListEmployeesResult>.Error(
                    "employee.status_invalid",
                    "Status must be Active, OnLeave, or Terminated.");
            }

            statusFilter = parsedStatus;
        }

        var totalCount = await _employees.CountAsync(
            new EmployeeListCountSpec(query.Search, roleFilter, statusFilter),
            ct);

        var employees = await _employees.ListAsync(
            new EmployeeListSpec(page, pageSize, query.Search, roleFilter, statusFilter),
            ct);

        var items = employees.Select(EmployeeMapper.ToResult).ToList();

        return new Result<ListEmployeesResult>.Success(new ListEmployeesResult
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        });
    }
}
