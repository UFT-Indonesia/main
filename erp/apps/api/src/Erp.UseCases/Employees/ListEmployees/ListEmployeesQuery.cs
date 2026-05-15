namespace Erp.UseCases.Employees.ListEmployees;

public sealed record ListEmployeesQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Role,
    string? Status);
