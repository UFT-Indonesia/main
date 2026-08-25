using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Employees.Common;
using Wolverine;

namespace Erp.UseCases.Employees.SetLeaveQuota;

/// <summary>
/// Owner-only per-employee override of one leave type's yearly entitlement. Zero is a real
/// setting; clearing (null days) returns the type to its default.
/// </summary>
public static class SetLeaveQuotaHandler
{
    public static async Task<Result<EmployeeResult>> Handle(
        SetLeaveQuotaCommand command,
        IRepository<Employee> employees,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (command.Caller.Role != EmployeeRole.Owner)
        {
            return new Result<EmployeeResult>.Error(
                ResultErrors.Forbidden, "Only an owner can change a leave quota.");
        }

        if (!Enum.TryParse<LeaveType>(command.Type, ignoreCase: true, out var type)
            || !Enum.IsDefined(type))
        {
            return new Result<EmployeeResult>.Error(
                "leave.type", "Leave type must be Annual, Sick, Permission, or Unpaid.");
        }

        var employee = await employees.GetByIdAsync(new EmployeeId(command.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<EmployeeResult>.NotFound("Employee was not found.");
        }

        try
        {
            employee.SetLeaveQuota(type, command.Days);
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
