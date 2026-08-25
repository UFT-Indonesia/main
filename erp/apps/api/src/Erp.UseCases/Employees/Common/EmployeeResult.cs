namespace Erp.UseCases.Employees.Common;

public sealed class EmployeeResult
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = default!;
    public string Nik { get; init; } = default!;
    public string? Npwp { get; init; }
    public decimal MonthlyWageAmount { get; init; }
    public string MonthlyWageCurrency { get; init; } = default!;
    public DateOnly EffectiveSalaryFrom { get; init; }
    public string Role { get; init; } = default!;
    public string Status { get; init; } = default!;
    public Guid? ParentId { get; init; }
    public DateOnly? TerminationDate { get; init; }

    /// <summary>First day of employment. Null for employees who predate the field.</summary>
    public DateOnly? HireDate { get; init; }

    /// <summary>Effective probation end — the override if set, otherwise three months from hire.</summary>
    public DateOnly? ProbationEndsOn { get; init; }

    /// <summary>
    /// Set only when an owner has pinned the date by hand. Exposed separately from
    /// <see cref="ProbationEndsOn"/> so the UI can say "default" versus "set by an owner".
    /// </summary>
    public DateOnly? ProbationEndsOnOverride { get; init; }

    /// <summary>Leave-type name to overridden entitlement. Types with no override are absent.</summary>
    public IReadOnlyDictionary<string, int> LeaveQuotaOverrides { get; init; } =
        new Dictionary<string, int>();
}
