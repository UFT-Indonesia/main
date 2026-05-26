using Erp.Core.Aggregates.Employees.Events;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeSalaryChangedHandler
{
    public static Task Handle(
        EmployeeSalaryChanged message,
        CancellationToken ct)
    {
        // TODO: Payroll notification, send to accounting system
        return Task.CompletedTask;
    }
}
