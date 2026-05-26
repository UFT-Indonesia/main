using Erp.Core.Aggregates.Employees.Events;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeBasicInfoChangedHandler
{
    public static Task Handle(
        EmployeeBasicInfoChanged message,
        CancellationToken ct)
    {
        // TODO: Audit trail, log changes for compliance
        return Task.CompletedTask;
    }
}
