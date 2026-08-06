namespace Erp.Web.Endpoints.Employees;

public sealed class CreateEmployeeRequest
{
    public string FullName { get; init; } = default!;
    public string Nik { get; init; } = default!;
    public string? Npwp { get; init; }
    public decimal MonthlyWageAmount { get; init; }
    public DateOnly EffectiveSalaryFrom { get; init; }
    public string Role { get; init; } = default!;
    public Guid? ParentId { get; init; }
}

public sealed class GetEmployeeByIdRequest
{
    public Guid Id { get; init; }
}

public sealed class UpdateEmployeeRouteRequest
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = default!;
    public string? Npwp { get; init; }
    /// <summary>Null leaves the wage unchanged. Managers must leave it null — only Owner may set pay.</summary>
    public decimal? MonthlyWageAmount { get; init; }
    /// <summary>Null leaves the salary effective date unchanged. Managers must leave it null.</summary>
    public DateOnly? EffectiveSalaryFrom { get; init; }
    public string Role { get; init; } = default!;
    public Guid? ParentId { get; init; }
}

public sealed class DeleteEmployeeRouteRequest
{
    public Guid Id { get; init; }
    public DateOnly? TerminationDate { get; init; }
}

public sealed class ListEmployeesRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? Role { get; init; }
    public string? Status { get; init; }
}

public sealed class EmployeeResponse
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = default!;
    public string Nik { get; init; } = default!;
    public string? Npwp { get; init; }
    /// <summary>Null for non-Owner callers — pay is redacted rather than omitted from the contract.</summary>
    public decimal? MonthlyWageAmount { get; init; }
    public string? MonthlyWageCurrency { get; init; }
    public DateOnly? EffectiveSalaryFrom { get; init; }
    public string Role { get; init; } = default!;
    public string Status { get; init; } = default!;
    public Guid? ParentId { get; init; }
    public DateOnly? TerminationDate { get; init; }
}

public sealed class ListEmployeesResponse
{
    public IReadOnlyList<EmployeeResponse> Items { get; init; } = Array.Empty<EmployeeResponse>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
