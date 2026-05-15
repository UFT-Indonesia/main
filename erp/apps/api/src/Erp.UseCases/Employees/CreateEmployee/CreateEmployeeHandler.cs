using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using NodaTime;

namespace Erp.UseCases.Employees.CreateEmployee;

public sealed class CreateEmployeeHandler
{
    private readonly IRepository<Employee> _employees;

    public CreateEmployeeHandler(IRepository<Employee> employees)
    {
        _employees = employees;
    }

    public async Task<Result<EmployeeResult>> Handle(
        CreateEmployeeCommand command,
        CancellationToken ct)
    {
        if (!Enum.TryParse<EmployeeRole>(command.Role, ignoreCase: true, out var role))
        {
            return new Result<EmployeeResult>.Error(
                "employee.role_invalid",
                "Role must be Owner, Manager, or Staff.");
        }

        Employee employee;
        try
        {
            var nik = Nik.Create(command.Nik);
            var npwp = string.IsNullOrWhiteSpace(command.Npwp) ? null : Npwp.Create(command.Npwp);
            var wage = Money.Idr(command.MonthlyWageAmount);
            var effectiveFrom = LocalDate.FromDateTime(
                command.EffectiveSalaryFrom.ToDateTime(TimeOnly.MinValue));
            var parentId = command.ParentId.HasValue
                ? new EmployeeId(command.ParentId.Value)
                : (EmployeeId?)null;

            employee = Employee.Create(
                command.FullName,
                nik,
                wage,
                effectiveFrom,
                role,
                parentId,
                npwp);
        }
        catch (DomainException ex)
        {
            return new Result<EmployeeResult>.Error(ex.Code ?? "employee.validation", ex.Message);
        }

        await _employees.AddAsync(employee, ct);
        return new Result<EmployeeResult>.Success(EmployeeMapper.ToResult(employee));
    }
}
