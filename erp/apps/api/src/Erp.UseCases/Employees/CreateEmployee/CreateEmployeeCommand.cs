namespace Erp.UseCases.Employees.CreateEmployee;

public sealed record CreateEmployeeCommand(
    string FullName,
    string Nik,
    string? Npwp,
    decimal MonthlyWageAmount,
    DateOnly EffectiveSalaryFrom,
    string Role,
    Guid? ParentId);
