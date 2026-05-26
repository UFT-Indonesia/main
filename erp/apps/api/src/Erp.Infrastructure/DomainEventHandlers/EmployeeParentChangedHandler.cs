using Erp.Core.Aggregates.Employees.Events;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeParentChangedHandler
{
    public static Task Handle(
        EmployeeParentChanged message,
        CancellationToken ct)
    {
        // TODO: Org chart update, recalculate reporting structure
        return Task.CompletedTask;
    }
}
