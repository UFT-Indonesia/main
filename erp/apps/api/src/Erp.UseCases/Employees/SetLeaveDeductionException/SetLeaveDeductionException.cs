using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Employees.Common;
using Wolverine;

namespace Erp.UseCases.Employees.SetLeaveDeductionException;

/// <summary>Both null clears the exception. Set a flat amount per cut day (0 exempts) or a custom divisor, not both.</summary>
public sealed record SetLeaveDeductionExceptionCommand(Guid EmployeeId, decimal? FlatAmountPerDay, int? Divisor, Caller Caller);

/// <summary>Owner-only: a pricing setting for money taken from a salary, same gate as <c>SetLeaveQuotaHandler</c>.</summary>
public static class SetLeaveDeductionExceptionHandler
{
    public static async Task<Result<EmployeeResult>> Handle(
        SetLeaveDeductionExceptionCommand command,
        IRepository<Employee> employees,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (command.Caller.Role != EmployeeRole.Owner)
        {
            return new Result<EmployeeResult>.Error(
                ResultErrors.Forbidden, "Only an owner can change how a leave deduction is priced.");
        }

        var employee = await employees.GetByIdAsync(new EmployeeId(command.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<EmployeeResult>.NotFound("Employee was not found.");
        }

        try
        {
            employee.SetLeaveDeductionException(command.FlatAmountPerDay, command.Divisor);
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
