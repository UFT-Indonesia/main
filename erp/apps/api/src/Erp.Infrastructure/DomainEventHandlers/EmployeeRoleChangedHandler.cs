using Erp.Core.Aggregates.Employees.Events;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeRoleChangedHandler
{
    public static Task Handle(
        EmployeeRoleChanged message,
        CancellationToken ct)
    {
        // TODO: Permission sync, update access control lists
        return Task.CompletedTask;
    }
}
