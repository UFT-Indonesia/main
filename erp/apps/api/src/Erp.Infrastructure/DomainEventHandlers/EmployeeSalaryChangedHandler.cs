using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Payroll.Common;
using Wolverine;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeSalaryChangedHandler
{
    public static async Task Handle(
        EmployeeSalaryChanged message,
        IRepository<EmployeeAuditLog> auditLogs,
        IRepository<EmployeeSalaryHistory> salaries,
        Envelope envelope,
        CancellationToken ct)
    {
        await RecordHistoryAsync(message, salaries, ct);

        await EmployeeAuditLogWriter.WriteAsync(
            auditLogs,
            envelope,
            new EmployeeId(message.EmployeeId),
            message.EventType,
            message.RaisedAt,
            oldValue: new SalaryAuditValue(
                message.OldMonthlyWage.Amount, message.OldMonthlyWage.Currency, message.OldEffectiveFrom.ToDateOnly()),
            newValue: new SalaryAuditValue(
                message.NewMonthlyWage.Amount, message.NewMonthlyWage.Currency, message.NewEffectiveFrom.ToDateOnly()),
            ct);
    }

    /// <summary>
    /// Leave deductions price each day with the salary in effect on it (GSS03 decision 12). History was
    /// seeded at launch; an employee hired later has no rows until their first change, which writes the
    /// salary they held before it as well. A change on an existing date replaces that row.
    /// </summary>
    private static async Task RecordHistoryAsync(
        EmployeeSalaryChanged message, IRepository<EmployeeSalaryHistory> salaries, CancellationToken ct)
    {
        var rows = (await salaries.ListAsync(new EmployeeSalaryHistorySpec(message.EmployeeId), ct)).ToList();

        if (rows.Count == 0)
        {
            var before = new EmployeeSalaryHistory(
                message.EmployeeId, message.OldEffectiveFrom, message.OldMonthlyWage.Amount);
            await salaries.AddAsync(before, ct);
            rows.Add(before);
        }

        var sameDate = rows.FirstOrDefault(r => r.EffectiveFrom == message.NewEffectiveFrom);
        if (sameDate is null)
        {
            await salaries.AddAsync(
                new EmployeeSalaryHistory(message.EmployeeId, message.NewEffectiveFrom, message.NewMonthlyWage.Amount), ct);
            return;
        }

        sameDate.Correct(message.NewMonthlyWage.Amount);
        await salaries.UpdateAsync(sameDate, ct);
    }
}
