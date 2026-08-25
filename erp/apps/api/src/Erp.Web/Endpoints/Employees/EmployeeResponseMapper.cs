using Erp.UseCases.Common;
using Erp.UseCases.Employees.Common;

namespace Erp.Web.Endpoints.Employees;

internal static class EmployeeResponseMapper
{
    /// <summary>
    /// The directory is readable by every employee so pickers can name people, but personal
    /// details are stripped for anyone without standing, and pay stays Owner-only.
    /// </summary>
    internal static EmployeeResponse ToResponse(EmployeeResult result, Caller caller)
    {
        var showWage = EmployeeVisibility.CanReadWage(caller);
        var showDetails = EmployeeVisibility.CanReadDetails(caller, result);

        return new EmployeeResponse
        {
            Id = result.Id,
            FullName = result.FullName,
            Role = result.Role,
            Status = result.Status,
            Nik = showDetails ? result.Nik : null,
            Npwp = showDetails ? result.Npwp : null,
            MonthlyWageAmount = showWage ? result.MonthlyWageAmount : null,
            MonthlyWageCurrency = showWage ? result.MonthlyWageCurrency : null,
            EffectiveSalaryFrom = showWage ? result.EffectiveSalaryFrom : null,
            ParentId = showDetails ? result.ParentId : null,
            TerminationDate = showDetails ? result.TerminationDate : null,
            HireDate = showDetails ? result.HireDate : null,
            ProbationEndsOn = showDetails ? result.ProbationEndsOn : null,
            ProbationEndsOnOverride = showDetails ? result.ProbationEndsOnOverride : null,
            LeaveQuotaOverrides = showDetails ? result.LeaveQuotaOverrides : null,
        };
    }
}
