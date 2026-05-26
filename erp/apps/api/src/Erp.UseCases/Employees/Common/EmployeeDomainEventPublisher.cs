using Erp.Core.Aggregates.Employees.Events;
using Erp.SharedKernel.Domain;
using Wolverine;

namespace Erp.UseCases.Employees.Common;

internal static class EmployeeDomainEventPublisher
{
    internal static async Task PublishAsync(IReadOnlyCollection<IDomainEvent> domainEvents, IMessageBus bus)
    {
        foreach (var domainEvent in domainEvents)
        {
            switch (domainEvent)
            {
                case EmployeeCreated employeeCreated:
                    await bus.PublishAsync(employeeCreated);
                    break;
                case EmployeeBasicInfoChanged employeeBasicInfoChanged:
                    await bus.PublishAsync(employeeBasicInfoChanged);
                    break;
                case EmployeeSalaryChanged employeeSalaryChanged:
                    await bus.PublishAsync(employeeSalaryChanged);
                    break;
                case EmployeeRoleChanged employeeRoleChanged:
                    await bus.PublishAsync(employeeRoleChanged);
                    break;
                case EmployeeParentChanged employeeParentChanged:
                    await bus.PublishAsync(employeeParentChanged);
                    break;
                case EmployeeTerminated employeeTerminated:
                    await bus.PublishAsync(employeeTerminated);
                    break;
            }
        }
    }
}
