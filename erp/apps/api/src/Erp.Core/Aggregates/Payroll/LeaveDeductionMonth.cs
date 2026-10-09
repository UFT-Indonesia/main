using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Domain.Errors;
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

    public LeaveDeductionMonth(LocalDate month, int divisor, Guid closedByUserId, string closedByName, Instant closedAtUtc)
    {
        Month = month;
        Divisor = divisor;
        ClosedByUserId = closedByUserId;
        ClosedByName = closedByName;
        ClosedAtUtc = closedAtUtc;
    }

    /// <summary>First day of the month. The key.</summary>
    public LocalDate Month { get; private set; }

    /// <summary>
    /// The company divisor the month was closed with. A day added to the month by a correction after
    /// close is priced with it, not today's (GSS03 follow-up Q10).
    /// </summary>
    public int Divisor { get; private set; }

    public Guid ClosedByUserId { get; private set; }

    public string ClosedByName { get; private set; } = default!;

    public Instant ClosedAtUtc { get; private set; }

    public static LocalDate MonthOf(LocalDate date) => new(date.Year, date.Month, 1);

    public static LocalDate EndOf(LocalDate month) => month.PlusMonths(1).PlusDays(-1);
}

/// <summary>An employee's deduction exception as it stood when a month closed (GSS03 follow-up Q10).</summary>
public sealed class LeaveDeductionMonthException
{
    // EF Core constructor.
    private LeaveDeductionMonthException() { }

    public LeaveDeductionMonthException(LocalDate month, Guid employeeId, decimal? flatAmountPerDay, int? divisor)
    {
        Month = month;
        EmployeeId = employeeId;
        FlatAmountPerDay = flatAmountPerDay;
        Divisor = divisor;
    }

    public LocalDate Month { get; private set; }

    public Guid EmployeeId { get; private set; }

    public decimal? FlatAmountPerDay { get; private set; }

    public int? Divisor { get; private set; }

    public Employees.LeaveDeductionException? ToException() =>
        Employees.LeaveDeductionException.Create(FlatAmountPerDay, Divisor);
}

/// <summary>
/// One leave day in a closed month (decision 15): free or cut, with the salary and rate it was priced at.
/// <list type="bullet">
/// <item>Written for every approved leave day when the month closes (<see cref="PaidAtClose"/>), and by an
/// approval or Owner correction that lands in an already-closed month afterwards (GSS03 follow-up Q6, Q8).</item>
/// <item>Never edited. A correction that removes the day, or changes its charge, marks it superseded
/// and leaves it as the record of what was paid.</item>
/// </list>
/// </summary>
public sealed class LeaveDeductionLine
{
    // EF Core constructor.
    private LeaveDeductionLine() { }

    public LeaveDeductionLine(
        LocalDate month, Guid employeeId, Guid leaveRequestId, LocalDate date, LeaveType type,
        decimal freeDays, decimal cutDays, decimal salary, decimal dailyRate,
        bool paidAtClose = true, LocalDate? lateTargetMonth = null)
    {
        Id = Guid.NewGuid();
        Month = month;
        EmployeeId = employeeId;
        LeaveRequestId = leaveRequestId;
        Date = date;
        Type = type;
        FreeDays = freeDays;
        CutDays = cutDays;
        Salary = salary;
        DailyRate = dailyRate;
        PaidAtClose = paidAtClose;
        LateTargetMonth = lateTargetMonth;
    }

    public Guid Id { get; private set; }

    public LocalDate Month { get; private set; }

    public Guid EmployeeId { get; private set; }

    public Guid LeaveRequestId { get; private set; }

    public LocalDate Date { get; private set; }

    public LeaveType Type { get; private set; }

    public decimal FreeDays { get; private set; }

    public decimal CutDays { get; private set; }

    public decimal Salary { get; private set; }

    public decimal DailyRate { get; private set; }

    /// <summary>
    /// True when written by closing the month: its amount is part of what was paid. False when added
    /// to the closed month afterwards; its money, if any, went to <see cref="LateTargetMonth"/>.
    /// </summary>
    public bool PaidAtClose { get; private set; }

    /// <summary>Set on a line added after close that carries a cut: the open month that charged it.</summary>
    public LocalDate? LateTargetMonth { get; private set; }

    /// <summary>Set when a correction after close removed this day or changed its charge.</summary>
    public Instant? SupersededAtUtc { get; private set; }

    public string? SupersededByName { get; private set; }

    /// <summary>The open month that took the correction's money, when it moved any.</summary>
    public LocalDate? SupersededTargetMonth { get; private set; }

    public bool IsActive => SupersededAtUtc is null;

    /// <summary>Exact, unrounded: rounding happens once per employee per month.</summary>
    public decimal ExactAmount => CutDays * DailyRate;

    public void Supersede(string byName, Instant atUtc, LocalDate? targetMonth)
    {
        if (!IsActive)
        {
            throw new DomainException("payroll.line_superseded", "This leave day was already corrected.");
        }

        SupersededByName = byName;
        SupersededAtUtc = atUtc;
        SupersededTargetMonth = targetMonth;
    }
}

/// <summary>
/// Money moved into an open month by an Owner's correction of leave in a closed month (GSS03 follow-up
/// Q9): one row per corrected leave day that carried a cut. Positive charges, negative refunds.
/// </summary>
public sealed class LeaveDeductionCorrection
{
    // EF Core constructor.
    private LeaveDeductionCorrection() { }

    public LeaveDeductionCorrection(
        LocalDate targetMonth, LocalDate sourceMonth, Guid employeeId, Guid leaveRequestId, LocalDate date,
        LeaveType type, decimal cutDays, decimal dailyRate, string reason, Guid byUserId, string byName, Instant atUtc)
    {
        Id = Guid.NewGuid();
        TargetMonth = targetMonth;
        SourceMonth = sourceMonth;
        EmployeeId = employeeId;
        LeaveRequestId = leaveRequestId;
        Date = date;
        Type = type;
        CutDays = cutDays;
        DailyRate = dailyRate;
        Reason = reason;
        ByUserId = byUserId;
        ByName = byName;
        AtUtc = atUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>The open month that pays or refunds it.</summary>
    public LocalDate TargetMonth { get; private set; }

    /// <summary>The closed month the corrected day falls in.</summary>
    public LocalDate SourceMonth { get; private set; }

    public Guid EmployeeId { get; private set; }

    public Guid LeaveRequestId { get; private set; }

    public LocalDate Date { get; private set; }

    public LeaveType Type { get; private set; }

    /// <summary>Signed: positive adds a cut, negative refunds one.</summary>
    public decimal CutDays { get; private set; }

    public decimal DailyRate { get; private set; }

    public string Reason { get; private set; } = default!;

    public Guid ByUserId { get; private set; }

    public string ByName { get; private set; } = default!;

    public Instant AtUtc { get; private set; }

    /// <summary>Exact, signed.</summary>
    public decimal Amount => CutDays * DailyRate;
}

/// <summary>
/// An Owner's free-form line on an open month (GSS03 follow-up Q12/Q15): a refund (negative) or an extra
/// deduction (positive), with a reason. Deleted and re-added, never edited; frozen when the month closes.
/// </summary>
public sealed class LeaveDeductionAdjustment
{
    // EF Core constructor.
    private LeaveDeductionAdjustment() { }

    public LeaveDeductionAdjustment(
        LocalDate month, Guid employeeId, decimal amount, string reason, Guid byUserId, string byName, Instant atUtc)
    {
        if (amount == 0m)
        {
            throw new DomainException("payroll.adjustment_amount", "An adjustment needs a non-zero amount.");
        }

        if (amount != Math.Round(amount, 0))
        {
            throw new DomainException("payroll.adjustment_amount", "An adjustment is a whole rupiah amount.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("payroll.adjustment_reason", "An adjustment needs a reason.");
        }

        Id = Guid.NewGuid();
        Month = month;
        EmployeeId = employeeId;
        Amount = amount;
        Reason = reason.Trim();
        ByUserId = byUserId;
        ByName = byName;
        AtUtc = atUtc;
    }

    public Guid Id { get; private set; }

    public LocalDate Month { get; private set; }

    public Guid EmployeeId { get; private set; }

    /// <summary>Whole rupiah. Positive deducts, negative refunds.</summary>
    public decimal Amount { get; private set; }

    public string Reason { get; private set; } = default!;

    public Guid ByUserId { get; private set; }

    public string ByName { get; private set; } = default!;

    public Instant AtUtc { get; private set; }
}
