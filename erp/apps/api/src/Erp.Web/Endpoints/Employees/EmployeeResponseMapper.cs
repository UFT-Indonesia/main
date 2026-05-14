using Erp.UseCases.Employees.Common;

namespace Erp.Web.Endpoints.Employees;

internal static class EmployeeResponseMapper
{
    internal static EmployeeResponse ToResponse(EmployeeResult result) => new()
    {
        Id = result.Id,
        FullName = result.FullName,
        Nik = result.Nik,
        Npwp = result.Npwp,
        MonthlyWageAmount = result.MonthlyWageAmount,
        MonthlyWageCurrency = result.MonthlyWageCurrency,
        EffectiveSalaryFrom = result.EffectiveSalaryFrom,
        Role = result.Role,
        Status = result.Status,
        ParentId = result.ParentId,
        TerminationDate = result.TerminationDate,
    };
}
