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
}
