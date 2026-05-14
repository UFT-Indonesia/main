namespace Erp.UseCases.Employees.UpdateEmployee;

public sealed record UpdateEmployeeCommand(
    Guid EmployeeId,
    string FullName,
    string? Npwp,
    decimal MonthlyWageAmount,
    DateOnly EffectiveSalaryFrom,
    string Role,
    Guid? ParentId);
