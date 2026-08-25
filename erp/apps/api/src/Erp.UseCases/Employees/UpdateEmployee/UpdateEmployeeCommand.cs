using Erp.UseCases.Common;

namespace Erp.UseCases.Employees.UpdateEmployee;

/// <summary>
/// Null <paramref name="MonthlyWageAmount"/>/<paramref name="EffectiveSalaryFrom"/> leave the
/// current salary untouched, and a null <paramref name="HireDate"/> leaves the hire date alone —
/// there is deliberately no way to clear one back to null through this command.
/// </summary>
public sealed record UpdateEmployeeCommand(
    Guid EmployeeId,
    string FullName,
    string? Npwp,
    decimal? MonthlyWageAmount,
    DateOnly? EffectiveSalaryFrom,
    string Role,
    Guid? ParentId,
    DateOnly? HireDate,
    Caller Caller);
