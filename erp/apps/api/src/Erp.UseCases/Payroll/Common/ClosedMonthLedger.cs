using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Interfaces;
using Erp.UseCases.Leave.Common;
using NodaTime;

namespace Erp.UseCases.Payroll.Common;

/// <summary>Who corrects leave after its payroll month closed, and why (GSS03 follow-up Q8).</summary>
public sealed record PayrollCorrection(string Reason, Guid ByUserId, string ByName);

/// <summary>One leave day in a closed month whose money moves: positive charges a cut, negative refunds one.</summary>
public sealed record ClosedDayMoney(LocalDate SourceMonth, LocalDate Date, LeaveType Type, decimal CutDays, decimal DailyRate)
{
    public decimal Amount => CutDays * DailyRate;
}

/// <summary>
/// What approving, editing or cancelling a request does to its days in closed months, before anything is
/// written. <see cref="OverQuotaDaysAfter"/> covers every day of the request, open months included.
/// </summary>
public sealed record ClosedDaysPlan(
    IReadOnlyList<LeaveDeductionLine> Superseded,
    IReadOnlyList<LeaveDeductionLine> Added,
    IReadOnlyList<ClosedDayMoney> Money,
    LocalDate? TargetMonth,
    decimal OverQuotaDaysAfter)
{
    public static ClosedDaysPlan Empty { get; } = new([], [], [], null, 0m);

    public decimal NetAmount => Money.Sum(m => m.Amount);
}

/// <summary>
/// Keeps GSS03 decision 15 ("closed days keep their label forever") true after a month closes:
/// <list type="bullet">
/// <item>an approval into a closed month stamps its days there, as close would have (follow-up Q6);</item>
/// <item>an Owner's correction supersedes the days it removes or reshapes, stamps the ones it adds, and
/// moves the money difference to the first open month (Q8–Q10). Other requests' closed labels never move (Q11).</item>
/// </list>
/// </summary>
public sealed class ClosedMonthLedger(
    IRepository<LeaveDeductionLine> lines,
    IReadRepository<LeaveDeductionMonth> months,
    IReadRepository<LeaveDeductionMonthException> monthExceptions,
    IRepository<LeaveDeductionCorrection> corrections,
    IReadRepository<LeaveRequest> leaveRequests,
    IReadRepository<EmployeeSalaryHistory> salaries,
    AttendanceDayPolicy policy)
{
    private IReadOnlyList<LeaveDeductionMonth>? _closed;

    public async Task<IReadOnlyList<LeaveDeductionMonth>> ClosedAsync(CancellationToken ct) =>
        _closed ??= (await months.ListAsync(new ClosedDeductionMonthsSpec(), ct)).ToList();

    /// <summary>The earliest closed month holding a workday of the range, or null.</summary>
    public async Task<LocalDate?> FirstClosedMonthAsync(LocalDate start, LocalDate end, CancellationToken ct)
    {
        var closed = (await ClosedAsync(ct)).Select(m => m.Month).ToHashSet();
        return FirstClosedMonth(start, end, closed, policy);
    }

    internal static LocalDate? FirstClosedMonth(
        LocalDate start, LocalDate end, IReadOnlySet<LocalDate> closed, AttendanceDayPolicy policy)
    {
        if (closed.Count == 0)
        {
            return null;
        }

        foreach (var date in LeaveRequest.Workdays(start, end, policy))
        {
            var month = LeaveDeductionMonth.MonthOf(date);
            if (closed.Contains(month))
            {
                return month;
            }
        }

        return null;
    }

    /// <summary>
    /// Plans the request's closed-month days as it stands now (after the edit, approval or cancellation
    /// was applied in memory). <paramref name="tracked"/> loads its existing lines for update; a preview
    /// passes false and writes nothing.
    /// </summary>
    public async Task<ClosedDaysPlan> PlanAsync(
        Employee employee, LeaveRequest request, LocalDate today, bool tracked, CancellationToken ct)
    {
        if (employee.Role == EmployeeRole.Owner)
        {
            return ClosedDaysPlan.Empty;
        }

        var closedMonths = await ClosedAsync(ct);
        var closed = closedMonths.Select(m => m.Month).ToHashSet();
        var mine = (await lines.ListAsync(new ActiveLinesOfRequestSpec(request.Id.Value, tracked), ct)).ToList();

        var approved = request.Status == LeaveRequestStatus.Approved;
        var allDays = approved
            ? LeaveRequest.Workdays(request.StartDate, request.EndDate, policy).ToList()
            : [];
        var charge = Math.Round(request.ChargePerWorkday(policy), LeaveDeductionCalculator.DayDecimals);

        // A closed day keeps its label only while it is still a day of the request with the same charge;
        // otherwise it is superseded, and re-stamped below if the request still covers it.
        var keep = mine.Where(l => allDays.Contains(l.Date) && l.FreeDays + l.CutDays == charge)
            .Select(l => l.Date).ToHashSet();
        var superseded = mine.Where(l => !keep.Contains(l.Date)).ToList();

        IReadOnlyList<LeaveDeductionDay> requestDays = [];
        if (approved && allDays.Count > 0)
        {
            var fromYear = request.StartDate.Year;
            var toYear = request.EndDate.Year;
            var others = await leaveRequests.ListAsync(
                new ApprovedLeaveForYearSpec([employee.Id], fromYear, toYear, request.Id), ct);
            var frozen = (await lines.ListAsync(
                    new DeductionLinesForEmployeesSpec([employee.Id.Value], fromYear, toYear), ct))
                .Where(l => !superseded.Any(s => s.Id == l.Id))
                .ToList();

            requestDays = LeaveDeductionEngine.Allocate(
                    employee,
                    others.Select(r => LeaveDeductionEngine.ToRequest(r, policy))
                        .Append(LeaveDeductionEngine.ToRequest(request, policy)),
                    frozen,
                    policy,
                    today)
                .Where(d => d.RequestId == request.Id.Value)
                .ToList();
        }

        var addedDays = requestDays
            .Where(d => closed.Contains(LeaveDeductionMonth.MonthOf(d.Date)) && !keep.Contains(d.Date))
            .ToList();

        // Money only moves for cut days. It always lands on the first open month: closing runs oldest
        // first, so that is the month after the latest closed one (Q9).
        var target = closedMonths.Count == 0 ? (LocalDate?)null : closedMonths.Max(m => m.Month).PlusMonths(1);
        var money = new List<ClosedDayMoney>();
        foreach (var line in superseded.Where(l => l.CutDays > 0))
        {
            money.Add(new ClosedDayMoney(line.Month, line.Date, line.Type, -line.CutDays, line.DailyRate));
        }

        var added = new List<LeaveDeductionLine>();
        if (addedDays.Count > 0)
        {
            var monthsTouched = addedDays.Select(d => LeaveDeductionMonth.MonthOf(d.Date)).Distinct().ToList();
            var snapshots = (await monthExceptions.ListAsync(new MonthExceptionsSpec(monthsTouched, employee.Id.Value), ct))
                .ToDictionary(x => x.Month);
            var history = (await salaries.ListAsync(new SalaryHistoryOfEmployeesSpec([employee.Id.Value]), ct))
                .Select(h => new SalaryPoint(h.EffectiveFrom, h.Amount))
                .ToList();
            var divisors = closedMonths.ToDictionary(m => m.Month, m => m.Divisor);

            foreach (var day in addedDays)
            {
                var month = LeaveDeductionMonth.MonthOf(day.Date);
                var salary = LeaveDeductionCalculator.SalaryOn(history, day.Date, employee.MonthlyWage.Amount);

                // Priced as the month was when it closed: its divisor and the employee's exception then (Q10).
                var rate = LeaveDeductionCalculator.DailyRate(
                    salary, divisors[month], snapshots.GetValueOrDefault(month)?.ToException());
                added.Add(new LeaveDeductionLine(
                    month, employee.Id.Value, request.Id.Value, day.Date, day.Type, day.FreeDays, day.CutDays,
                    salary, rate, paidAtClose: false, lateTargetMonth: day.CutDays > 0 ? target : null));

                if (day.CutDays > 0)
                {
                    money.Add(new ClosedDayMoney(month, day.Date, day.Type, day.CutDays, rate));
                }
            }
        }

        return new ClosedDaysPlan(
            superseded, added, money, money.Count > 0 ? target : null, requestDays.Sum(d => d.CutDays));
    }

    /// <summary>
    /// Writes a plan. Without a <paramref name="correction"/> it may only stamp free days — an approval
    /// that would cut inside a closed month is refused before it gets here (Q4).
    /// </summary>
    public async Task ApplyAsync(
        ClosedDaysPlan plan, LeaveRequest request, PayrollCorrection? correction, Instant now, CancellationToken ct)
    {
        if (correction is null && (plan.Money.Count > 0 || plan.Superseded.Count > 0))
        {
            throw new InvalidOperationException("Only an Owner's correction can change closed payroll days.");
        }

        foreach (var line in plan.Superseded)
        {
            line.Supersede(correction!.ByName, now, line.CutDays > 0 ? plan.TargetMonth : null);
        }

        if (plan.Superseded.Count > 0)
        {
            await lines.UpdateRangeAsync(plan.Superseded, ct);
        }

        if (plan.Added.Count > 0)
        {
            await lines.AddRangeAsync(plan.Added, ct);
        }

        if (correction is not null && plan.Money.Count > 0)
        {
            await corrections.AddRangeAsync(
                plan.Money.Select(m => new LeaveDeductionCorrection(
                    plan.TargetMonth!.Value, m.SourceMonth, request.EmployeeId.Value, request.Id.Value, m.Date, m.Type,
                    m.CutDays, m.DailyRate, correction.Reason, correction.ByUserId, correction.ByName, now)),
                ct);
        }
    }
}
