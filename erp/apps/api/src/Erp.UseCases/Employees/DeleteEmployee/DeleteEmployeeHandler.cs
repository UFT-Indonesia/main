using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Employees.DeleteEmployee;

public static class DeleteEmployeeHandler
{
    public static async Task<Result<EmployeeResult>> Handle(
        DeleteEmployeeCommand command,
        IRepository<Employee> employees,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct)
    {
        var employee = await employees.GetByIdAsync(new EmployeeId(command.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<EmployeeResult>.NotFound("Employee was not found.");
        }

        var terminationDate = command.TerminationDate.HasValue
            ? LocalDate.FromDateTime(command.TerminationDate.Value.ToDateTime(TimeOnly.MinValue))
            : clock.GetCurrentInstant().InUtc().Date;

        try
        {
            employee.Terminate(terminationDate);
        }
        catch (DomainException ex)
        {
            return new Result<EmployeeResult>.Error(ex.Code ?? "employee.validation", ex.Message);
        }

        await employees.UpdateAsync(employee, ct);
        foreach (var domainEvent in employee.DomainEvents.OfType<EmployeeTerminated>())
        {
            await bus.PublishAsync(domainEvent);
        }

        return new Result<EmployeeResult>.Success(EmployeeMapper.ToResult(employee));
    }
}
