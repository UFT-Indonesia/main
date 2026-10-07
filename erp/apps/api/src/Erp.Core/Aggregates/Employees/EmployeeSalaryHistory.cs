namespace Erp.Core.Aggregates.Employees;

/// <summary>
/// One salary an employee had from <see cref="EffectiveFrom"/> until the next row (GSS03 decision
/// 12). A leave day is priced with the salary in effect on its own date, so a raise entered late
/// never touches an earlier month. The (employee, date) pair is the key: a same-date correction
/// replaces the row.
/// </summary>
public sealed class EmployeeSalaryHistory
{
    // EF Core constructor.
    private EmployeeSalaryHistory() { }

    public EmployeeSalaryHistory(Guid employeeId, NodaTime.LocalDate effectiveFrom, decimal amount)
    {
        EmployeeId = employeeId;
        EffectiveFrom = effectiveFrom;
        Amount = amount;
    }

    public Guid EmployeeId { get; private set; }

    public NodaTime.LocalDate EffectiveFrom { get; private set; }

    public decimal Amount { get; private set; }

    public void Correct(decimal amount) => Amount = amount;
}
