using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Employees.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Employees.SetProbationEnd;

/// <summary>
/// The Owner's direct edit of a probation end date — the escape hatch the extension workflow
/// routes around. Owner-only: a Manager who wants this files a request instead.
/// </summary>
public static class SetProbationEndHandler
{
    public static async Task<Result<EmployeeResult>> Handle(
        SetProbationEndCommand command,
        IRepository<Employee> employees,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (command.Caller.Role != EmployeeRole.Owner)
        {
            return new Result<EmployeeResult>.Error(
                ResultErrors.Forbidden, "Only an owner can change a probation end date.");
        }

        var employee = await employees.GetByIdAsync(new EmployeeId(command.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<EmployeeResult>.NotFound("Employee was not found.");
        }

        try
        {
            employee.OverrideProbationEnd(
                command.EndsOn.HasValue ? LocalDate.FromDateOnly(command.EndsOn.Value) : null);
        }
        catch (DomainException ex)
        {
            return new Result<EmployeeResult>.Error(ex.Code ?? "employee.validation", ex.Message);
        }

        await employees.UpdateAsync(employee, ct);
        await EmployeeDomainEventPublisher.PublishAsync(employee.DomainEvents, bus, command.Caller);

        return new Result<EmployeeResult>.Success(EmployeeMapper.ToResult(employee));
    }
}
