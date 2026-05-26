using Erp.Core.Aggregates.Employees.Events;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeCreatedHandler
{
    public static Task Handle(
        EmployeeCreated message,
        CancellationToken ct)
    {
        // TODO: Log creation, send webhook, notify external systems
        return Task.CompletedTask;
    }
}
