using Erp.Core.Aggregates.Employees;

namespace Erp.UseCases.Employees.Common;

internal static class EmployeeMapper
{
    internal static EmployeeResult ToResult(Employee employee) => new()
    {
        Id = employee.Id.Value,
        FullName = employee.FullName,
        Nik = employee.Nik.Value,
        Npwp = employee.Npwp?.Value,
        MonthlyWageAmount = employee.MonthlyWage.Amount,
        MonthlyWageCurrency = employee.MonthlyWage.Currency,
        EffectiveSalaryFrom = DateOnly.FromDateTime(
            employee.EffectiveSalaryFrom.ToDateTimeUnspecified()),
        Role = employee.Role.ToString(),
        Status = employee.Status.ToString(),
        ParentId = employee.ParentId?.Value,
        TerminationDate = employee.TerminationDate.HasValue
            ? DateOnly.FromDateTime(employee.TerminationDate.Value.ToDateTimeUnspecified())
            : null,
    };
}
