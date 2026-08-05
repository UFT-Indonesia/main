using System.Security.Claims;
using Erp.Core.Aggregates.Employees;
using Erp.UseCases.Employees.Common;

namespace Erp.Web.Endpoints.Employees;

internal static class EmployeeResponseMapper
{
    /// <summary>
    /// Pay fields are redacted for anyone but an Owner — Managers can read the employee
    /// list to reach the edit form, but salary stays Owner-only.
    /// </summary>
    internal static EmployeeResponse ToResponse(EmployeeResult result, ClaimsPrincipal caller)
    {
        var showWage = caller.IsInRole(nameof(EmployeeRole.Owner));

        return new EmployeeResponse
        {
            Id = result.Id,
            FullName = result.FullName,
            Nik = result.Nik,
            Npwp = result.Npwp,
            MonthlyWageAmount = showWage ? result.MonthlyWageAmount : null,
            MonthlyWageCurrency = showWage ? result.MonthlyWageCurrency : null,
            EffectiveSalaryFrom = showWage ? result.EffectiveSalaryFrom : null,
            Role = result.Role,
            Status = result.Status,
            ParentId = result.ParentId,
            TerminationDate = result.TerminationDate,
        };
    }
}
