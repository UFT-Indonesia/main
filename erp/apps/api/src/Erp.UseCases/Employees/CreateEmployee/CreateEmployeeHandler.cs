using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Employees.CreateEmployee;

public static class CreateEmployeeHandler
{
    public static async Task<Result<EmployeeResult>> Handle(
        CreateEmployeeCommand command,
        IRepository<Employee> employees,
        IEmployeeHierarchyLookup hierarchy,
        IMessageBus bus,
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

            var parentAncestors = await EmployeeHierarchyService.ResolveAncestorsForParentAsync(
                parentId, hierarchy, ct);

            employee = Employee.Create(
                command.FullName,
                nik,
                wage,
                effectiveFrom,
                role,
                parentId,
                npwp,
                parentAncestors: parentAncestors);
        }
        catch (DomainException ex)
        {
            return new Result<EmployeeResult>.Error(ex.Code ?? "employee.validation", ex.Message);
        }

        await employees.AddAsync(employee, ct);
        foreach (var domainEvent in employee.DomainEvents.OfType<EmployeeCreated>())
        {
            await bus.PublishAsync(domainEvent);
        }

        return new Result<EmployeeResult>.Success(EmployeeMapper.ToResult(employee));
    }
}
