using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using NodaTime;

namespace Erp.Core.Aggregates.Payroll;

/// <summary>
/// One approved leave request, as the calculator sees it. <paramref name="EffectiveAt"/> is when it
/// last took the quota: its decision time, or its latest edit if that is later (GSS03 decision 6 —
/// an edit counts as approving again).
/// </summary>
public sealed record LeaveDeductionRequest(
    Guid RequestId, LeaveType Type, LocalDate Start, LocalDate End, decimal ChargePerWorkday, Instant EffectiveAt);

/// <summary>One workday of one request: how much of its charge is free and how much is cut.</summary>
public sealed record LeaveDeductionDay(Guid RequestId, LeaveType Type, LocalDate Date, decimal FreeDays, decimal CutDays);

/// <summary>A salary that took effect on <paramref name="EffectiveFrom"/> and held until the next point.</summary>
public sealed record SalaryPoint(LocalDate EffectiveFrom, decimal Amount);

/// <summary>
/// Pure money path of GSS03: which leave days cost salary, and how much. Nothing here reads a
/// database — the caller loads requests, caps, frozen labels and salary history and passes them in.
/// </summary>
public static class LeaveDeductionCalculator
{
    /// <summary>Rupiah rounding unit for an employee's month total (decision 9).</summary>
    public const decimal RoundingUnit = 1000m;

    /// <summary>Decimals a daily rate is kept to; matches <c>LeaveDeductionLine.DailyRate</c> (18,4).</summary>
    public const int RateDecimals = 4;

    /// <summary>Decimals a free/cut day is kept to; matches <c>LeaveDeductionLine</c> (8,4).</summary>
    public const int DayDecimals = 4;

    /// <summary>
    /// Splits every workday of every request into free and cut charge.
    /// <list type="bullet">
    /// <item>Requests take the cap in <see cref="LeaveDeductionRequest.EffectiveAt"/> order; within a
    /// request the earliest days are free and the last days are cut (decision 6).</item>
    /// <item><c>Unpaid</c> is cut in full; the cap is not spent on it (decision 4).</item>
    /// <item>A day already labelled by a closed month keeps its label, and only its free part spends
    /// the cap — cut days never do (decision 15).</item>
    /// </list>
    /// <paramref name="capFor"/> returns the yearly cap for a type, or null when uncapped (an Owner).
    /// </summary>
    public static IReadOnlyList<LeaveDeductionDay> Allocate(
        IEnumerable<LeaveDeductionRequest> requests,
        Func<LeaveType, int, decimal?> capFor,
        AttendanceDayPolicy policy,
        IReadOnlyDictionary<(Guid RequestId, LocalDate Date), (decimal Free, decimal Cut)>? frozen = null)
    {
        var spent = new Dictionary<(LeaveType, int), decimal>();
        var days = new List<LeaveDeductionDay>();

        foreach (var request in requests.OrderBy(r => r.EffectiveAt).ThenBy(r => r.RequestId))
        {
            foreach (var date in LeaveRequest.Workdays(request.Start, request.End, policy))
            {
                var charge = Math.Round(request.ChargePerWorkday, DayDecimals);
                decimal free;

                if (frozen is not null && frozen.TryGetValue((request.RequestId, date), out var label))
                {
                    free = label.Free;
                    charge = label.Free + label.Cut;
                }
                else if (capFor(request.Type, date.Year) is not { } cap)
                {
                    // Uncapped (an Owner) is never cut, Unpaid included (decision 5).
                    free = charge;
                }
                else if (request.Type == LeaveType.Unpaid)
                {
                    free = 0m;
                }
                else
                {
                    var left = Math.Max(cap - spent.GetValueOrDefault((request.Type, date.Year)), 0m);
                    free = Math.Min(charge, left);
                }

                if (request.Type != LeaveType.Unpaid)
                {
                    spent[(request.Type, date.Year)] = spent.GetValueOrDefault((request.Type, date.Year)) + free;
                }

                days.Add(new LeaveDeductionDay(request.RequestId, request.Type, date, free, charge - free));
            }
        }

        return days;
    }

    /// <summary>
    /// The salary in effect on <paramref name="date"/>: the latest point at or before it. A date
    /// before the first recorded point uses that first point — history only starts at launch.
    /// </summary>
    public static decimal SalaryOn(IReadOnlyList<SalaryPoint> history, LocalDate date, decimal currentSalary)
    {
        if (history.Count == 0)
        {
            return currentSalary;
        }

        var inEffect = history.Where(p => p.EffectiveFrom <= date).OrderByDescending(p => p.EffectiveFrom).FirstOrDefault();
        return (inEffect ?? history.OrderBy(p => p.EffectiveFrom).First()).Amount;
    }

    /// <summary>
    /// Price of one full cut day: the employee's flat exception, else salary ÷ their custom divisor,
    /// else salary ÷ the company divisor (decisions 1, 11). Rounded to <see cref="RateDecimals"/> — what the
    /// frozen line stores — so an open month previews exactly the figure its close will freeze.
    /// </summary>
    public static decimal DailyRate(decimal salary, int companyDivisor, LeaveDeductionException? exception) =>
        Math.Round(exception?.FlatAmountPerDay ?? salary / (exception?.Divisor ?? companyDivisor), RateDecimals);

    /// <summary>
    /// An employee's month total: exact amounts summed, then rounded toward zero to Rp 1.000 once
    /// (decision 9; GSS03 follow-up Q14). A cut rounds down; a refund (negative) rounds to the smaller refund.
    /// </summary>
    public static decimal MonthTotal(IEnumerable<decimal> exactAmounts) =>
        Math.Truncate(exactAmounts.Sum() / RoundingUnit) * RoundingUnit;
}
