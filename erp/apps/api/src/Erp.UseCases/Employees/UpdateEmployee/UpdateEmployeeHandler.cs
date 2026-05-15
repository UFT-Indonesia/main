using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using NodaTime;

namespace Erp.UseCases.Employees.UpdateEmployee;

public sealed class UpdateEmployeeHandler
{
    private readonly IRepository<Employee> _employees;

    public UpdateEmployeeHandler(IRepository<Employee> employees)
    {
        _employees = employees;
    }

    public async Task<Result<EmployeeResult>> Handle(
        UpdateEmployeeCommand command,
        CancellationToken ct)
    {
        if (!Enum.TryParse<EmployeeRole>(command.Role, ignoreCase: true, out var role))
        {
            return new Result<EmployeeResult>.Error(
                "employee.role_invalid",
                "Role must be Owner, Manager, or Staff.");
        }

        var employee = await _employees.GetByIdAsync(new EmployeeId(command.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<EmployeeResult>.NotFound("Employee was not found.");
        }

        try
        {
            var npwp = string.IsNullOrWhiteSpace(command.Npwp) ? null : Npwp.Create(command.Npwp);
            var newWage = Money.Idr(command.MonthlyWageAmount);
            var newEffectiveFrom = LocalDate.FromDateTime(
                command.EffectiveSalaryFrom.ToDateTime(TimeOnly.MinValue));
            var newParentId = command.ParentId.HasValue
                ? new EmployeeId(command.ParentId.Value)
                : (EmployeeId?)null;

            employee.UpdateBasicInfo(command.FullName, npwp);

            if (!Equals(employee.MonthlyWage, newWage)
                || employee.EffectiveSalaryFrom != newEffectiveFrom)
            {
                employee.ChangeSalary(newWage, newEffectiveFrom);
            }

            // Handle role/parent transitions in an order that satisfies invariants
            // for the simple cases. Cross-tier transitions (Owner <-> non-Owner)
            // may surface as DomainException due to current aggregate constraints.
            if (role == EmployeeRole.Owner)
            {
                if (employee.ParentId != newParentId)
                {
                    employee.AssignParent(newParentId);
                }
                if (employee.Role != role)
                {
                    employee.ChangeRole(role);
                }
            }
            else
            {
                if (employee.Role != role)
                {
                    employee.ChangeRole(role);
                }
                if (employee.ParentId != newParentId)
                {
                    employee.AssignParent(newParentId);
                }
            }
        }
        catch (DomainException ex)
        {
            return new Result<EmployeeResult>.Error(ex.Code ?? "employee.validation", ex.Message);
        }

        await _employees.UpdateAsync(employee, ct);
        return new Result<EmployeeResult>.Success(EmployeeMapper.ToResult(employee));
    }
}
