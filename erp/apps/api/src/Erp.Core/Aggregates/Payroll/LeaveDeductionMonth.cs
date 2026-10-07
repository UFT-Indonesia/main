using Erp.Core.Aggregates.Leave;
using NodaTime;

namespace Erp.Core.Aggregates.Payroll;

/// <summary>
/// A calendar month the Owner has closed on the Potongan Cuti page. Only closed months are stored — an
/// open month is the absence of a row and is computed live. Closing is irreversible (GSS03 decision 2).
/// </summary>
public sealed class LeaveDeductionMonth
{
    // EF Core constructor.
    private LeaveDeductionMonth() { }

    public LeaveDeductionMonth(LocalDate month, Guid closedByUserId, string closedByName, Instant closedAtUtc)
    {
        Month = month;
        ClosedByUserId = closedByUserId;
        ClosedByName = closedByName;
        ClosedAtUtc = closedAtUtc;
    }

    /// <summary>First day of the month. The key.</summary>
    public LocalDate Month { get; private set; }

    public Guid ClosedByUserId { get; private set; }

    public string ClosedByName { get; private set; } = default!;

    public Instant ClosedAtUtc { get; private set; }

    public static LocalDate MonthOf(LocalDate date) => new(date.Year, date.Month, 1);

    public static LocalDate EndOf(LocalDate month) => month.PlusMonths(1).PlusDays(-1);
}

/// <summary>
/// One leave day frozen when its month closed (decision 15): free or cut, with the salary and rate it
/// was priced at. Written for every approved leave day of the month, so the label never moves.
/// </summary>
public sealed class LeaveDeductionLine
{
    // EF Core constructor.
    private LeaveDeductionLine() { }

    public LeaveDeductionLine(
        LocalDate month, Guid employeeId, Guid leaveRequestId, LocalDate date, LeaveType type,
        decimal freeDays, decimal cutDays, decimal salary, decimal dailyRate)
    {
        Month = month;
        EmployeeId = employeeId;
        LeaveRequestId = leaveRequestId;
        Date = date;
        Type = type;
        FreeDays = freeDays;
        CutDays = cutDays;
        Salary = salary;
        DailyRate = dailyRate;
    }

    public LocalDate Month { get; private set; }

    public Guid EmployeeId { get; private set; }

    public Guid LeaveRequestId { get; private set; }

    public LocalDate Date { get; private set; }

    public LeaveType Type { get; private set; }

    public decimal FreeDays { get; private set; }

    public decimal CutDays { get; private set; }

    public decimal Salary { get; private set; }

    public decimal DailyRate { get; private set; }

    /// <summary>Exact, unrounded: rounding happens once per employee per month.</summary>
    public decimal ExactAmount => CutDays * DailyRate;
}
