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
    /// <summary>Required. Anchors probation, and through it the annual leave entitlement.</summary>
    public DateOnly? HireDate { get; init; }
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
    /// <summary>Null leaves the hire date unchanged. Managers must leave it null — Owner-only, like pay.</summary>
    public DateOnly? HireDate { get; init; }
}

public sealed class SetProbationEndRouteRequest
{
    public Guid Id { get; init; }
    /// <summary>Null clears the owner's override, restoring the three-month default.</summary>
    public DateOnly? EndsOn { get; init; }
}

public sealed class SetLeaveQuotaRouteRequest
{
    public Guid Id { get; init; }
    public string Type { get; init; } = default!;
    /// <summary>Null clears the override. Zero is a real setting and means "none of this type".</summary>
    public decimal? Days { get; init; }
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

/// <summary>
/// Fields are redacted rather than omitted, so the shape is stable whoever asks. Everything
/// nullable here is withheld from callers without standing — see EmployeeVisibility.
/// </summary>
public sealed class EmployeeResponse
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = default!;
    public string Role { get; init; } = default!;
    public string Status { get; init; } = default!;

    /// <summary>National ID. Null unless the caller may read this employee's details.</summary>
    public string? Nik { get; init; }
    public string? Npwp { get; init; }

    /// <summary>Null for non-Owner callers — pay is redacted rather than omitted from the contract.</summary>
    public decimal? MonthlyWageAmount { get; init; }
    public string? MonthlyWageCurrency { get; init; }
    public DateOnly? EffectiveSalaryFrom { get; init; }
    public Guid? ParentId { get; init; }
    public DateOnly? TerminationDate { get; init; }

    /// <summary>Null unless the caller may read this employee's details.</summary>
    public DateOnly? HireDate { get; init; }
    public DateOnly? ProbationEndsOn { get; init; }
    /// <summary>Set only when an owner pinned the date by hand, rather than it being the default.</summary>
    public DateOnly? ProbationEndsOnOverride { get; init; }
    /// <summary>Leave type to overridden entitlement; types on the default are absent.</summary>
    public IReadOnlyDictionary<string, decimal>? LeaveQuotaOverrides { get; init; }
}

public sealed class ListEmployeesResponse
{
    public IReadOnlyList<EmployeeResponse> Items { get; init; } = Array.Empty<EmployeeResponse>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
