using Erp.Core.Aggregates.Employees.Events;
using Erp.SharedKernel.Domain;
using Erp.UseCases.Common;
using Wolverine;

namespace Erp.UseCases.Employees.Common;

internal static class EmployeeDomainEventPublisher
{
    internal static async Task PublishAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents, IMessageBus bus, Caller caller)
    {
        // Stamped here, not in the handler: this runs inside the request, the handler doesn't.
        var options = new DeliveryOptions()
            .WithHeader(EmployeeAuditHeaders.ActorUserId, caller.UserId.ToString())
            .WithHeader(EmployeeAuditHeaders.ActorName, caller.Name);

        foreach (var domainEvent in domainEvents)
        {
            switch (domainEvent)
            {
                case EmployeeCreated employeeCreated:
                    await bus.PublishAsync(employeeCreated, options);
                    break;
                case EmployeeBasicInfoChanged employeeBasicInfoChanged:
                    await bus.PublishAsync(employeeBasicInfoChanged, options);
                    break;
                case EmployeeSalaryChanged employeeSalaryChanged:
                    await bus.PublishAsync(employeeSalaryChanged, options);
                    break;
                case EmployeeRoleChanged employeeRoleChanged:
                    await bus.PublishAsync(employeeRoleChanged, options);
                    break;
                case EmployeeParentChanged employeeParentChanged:
                    await bus.PublishAsync(employeeParentChanged, options);
                    break;
                case EmployeeTerminated employeeTerminated:
                    await bus.PublishAsync(employeeTerminated, options);
                    break;
            }
        }
    }
}
