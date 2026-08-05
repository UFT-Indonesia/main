namespace Erp.UseCases.Employees.UpdateEmployee;

/// <summary>Null <paramref name="MonthlyWageAmount"/>/<paramref name="EffectiveSalaryFrom"/> leave the current salary untouched.</summary>
public sealed record UpdateEmployeeCommand(
    Guid EmployeeId,
    string FullName,
    string? Npwp,
    decimal? MonthlyWageAmount,
    DateOnly? EffectiveSalaryFrom,
    string Role,
    Guid? ParentId);
