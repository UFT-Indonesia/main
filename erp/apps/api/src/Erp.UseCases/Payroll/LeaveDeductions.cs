using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Payroll.Common;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Erp.UseCases.Payroll;

/// <summary>Null <paramref name="Month"/> means the month holding today. Any date in the month is accepted.</summary>
public sealed record GetLeaveDeductionMonthQuery(DateOnly? Month, Caller Caller);

public sealed record CloseLeaveDeductionMonthCommand(DateOnly Month, Caller Caller);

public sealed record SetPayrollDivisorCommand(int Divisor, Caller Caller);

/// <summary>One leave day of one request: how much of it is free and how much is cut, and what that is worth.</summary>
public sealed record LeaveDeductionDayResult(
    DateOnly Date, string LeaveType, Guid LeaveRequestId, DateOnly RequestStart, DateOnly RequestEnd,
    decimal FreeDays, decimal CutDays, decimal Salary, decimal DailyRate, decimal Amount);

/// <summary>
/// One employee's month. <paramref name="Total"/> is the exact day amounts summed and rounded down to
/// Rp 1.000 once; the day amounts shown are exact.
/// </summary>
public sealed record LeaveDeductionEmployeeRow(
    Guid EmployeeId, string FullName, IReadOnlyDictionary<string, decimal> CutDaysByType, decimal CutDays,
    decimal Total, IReadOnlyList<LeaveDeductionDayResult> Days);

public sealed record PendingLeaveItem(
    Guid LeaveRequestId, Guid EmployeeId, string FullName, string LeaveType, DateOnly Start, DateOnly End);

public sealed record LeaveDeductionMonthResult(
    DateOnly Month, bool Closed, DateTimeOffset? ClosedAtUtc, string? ClosedByName, bool CanClose, int Divisor,
    DateOnly FirstMonth, decimal Total, IReadOnlyList<LeaveDeductionEmployeeRow> Rows,
    IReadOnlyList<PendingLeaveItem> Pending);

public sealed record PayrollSettingsResult(int Divisor, DateOnly FirstMonth);

internal static class LeaveDeductionMonths
{
    internal static bool IsValidMonth(LocalDate month) => month == LeaveDeductionMonth.MonthOf(month);

    /// <summary>A Manager's or Owner's view needs wage standing: the page and every amount on it are Owner-only.</summary>
    internal static bool CanSee(Caller caller) => caller.Role == EmployeeRole.Owner;

    private sealed record DayLine(
        Guid EmployeeId, string FullName, LeaveType Type, LocalDate Date, Guid RequestId,
        decimal Free, decimal Cut, decimal Salary, decimal Rate);

    internal static async Task<LeaveDeductionMonthResult> BuildAsync(
        LocalDate month,
        PayrollSettings settings,
        IReadRepository<LeaveDeductionMonth> months,
        IReadRepository<LeaveDeductionLine> lines,
        IReadRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        IReadRepository<EmployeeSalaryHistory> salaries,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        var end = LeaveDeductionMonth.EndOf(month);
        var today = DisplayZone.Today(clock);
        var closed = await months.FirstOrDefaultAsync(new DeductionMonthSpec(month), ct);

        List<DayLine> days;
        if (closed is not null)
        {
            var frozen = await lines.ListAsync(new DeductionLinesOfMonthSpec(month), ct);
            var names = (await employees.ListAsync(
                    new EmployeesByIdsSpec(frozen.Select(l => new EmployeeId(l.EmployeeId)).Distinct().ToList()), ct))
                .ToDictionary(e => e.Id.Value, e => e.FullName);
            days = frozen.Select(l => new DayLine(
                l.EmployeeId, names.GetValueOrDefault(l.EmployeeId, "?"), l.Type, l.Date, l.LeaveRequestId,
                l.FreeDays, l.CutDays, l.Salary, l.DailyRate)).ToList();
        }
        else
        {
            var priced = await LeaveDeductionEngine.PriceOpenMonthAsync(
                month, settings, leaveRequests, employees, salaries, lines, policy, today, ct);
            days = priced.Select(p => new DayLine(
                p.Employee.Id.Value, p.Employee.FullName, p.Day.Type, p.Day.Date, p.Day.RequestId,
                p.Day.FreeDays, p.Day.CutDays, p.Salary, p.Rate)).ToList();
        }

        var requestRanges = (await leaveRequests.ListAsync(
                new LeaveRequestsByIdsSpec(days.Select(d => new LeaveRequestId(d.RequestId)).Distinct().ToList()), ct))
            .ToDictionary(r => r.Id.Value, r => (r.StartDate, r.EndDate));

        var rows = days.GroupBy(d => d.EmployeeId).Select(group =>
        {
            var cutByType = group.Where(d => d.Cut > 0)
                .GroupBy(d => d.Type.ToString())
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Cut));
            var dayResults = group.OrderBy(d => d.Date).Select(d =>
            {
                var (start, finish) = requestRanges.GetValueOrDefault(d.RequestId, (d.Date, d.Date));
                return new LeaveDeductionDayResult(
                    d.Date.ToDateOnly(), d.Type.ToString(), d.RequestId, start.ToDateOnly(), finish.ToDateOnly(),
                    d.Free, d.Cut, d.Salary, d.Rate, d.Cut * d.Rate);
            }).ToList();

            return new LeaveDeductionEmployeeRow(
                group.Key, group.First().FullName, cutByType, cutByType.Values.Sum(),
                LeaveDeductionCalculator.MonthTotal(group.Select(d => d.Cut * d.Rate)), dayResults);
        }).OrderBy(r => r.FullName).ToList();

        var pending = closed is null ? await PendingAsync(month, end, leaveRequests, policy, ct) : [];

        var closedMonths = await LeaveDeductionEngine.ClosedMonthsAsync(months, ct);
        var canClose = closed is null
            && today > end
            && month >= settings.FirstMonth
            && (month == settings.FirstMonth || closedMonths.Contains(month.PlusMonths(-1)))
            && pending.Count == 0;

        return new LeaveDeductionMonthResult(
            month.ToDateOnly(), closed is not null, closed?.ClosedAtUtc.ToDateTimeOffset(), closed?.ClosedByName,
            canClose, settings.Divisor, settings.FirstMonth.ToDateOnly(), rows.Sum(r => r.Total), rows, pending);
    }

    /// <summary>Undecided leave with a workday in the month — close is blocked until each is decided (decision 7).</summary>
    internal static async Task<IReadOnlyList<PendingLeaveItem>> PendingAsync(
        LocalDate month, LocalDate end, IReadRepository<LeaveRequest> leaveRequests, AttendanceDayPolicy policy,
        CancellationToken ct) =>
        (await leaveRequests.ListAsync(new PendingLeaveOverlappingSpec(month, end), ct))
            .Where(r => r.Employee is { Role: not EmployeeRole.Owner }
                        && LeaveRequest.Workdays(Max(r.StartDate, month), Min(r.EndDate, end), policy).Any())
            .OrderBy(r => r.StartDate)
            .Select(r => new PendingLeaveItem(
                r.Id.Value, r.EmployeeId.Value, r.Employee!.FullName, r.Type.ToString(),
                r.StartDate.ToDateOnly(), r.EndDate.ToDateOnly()))
            .ToList();

    private static LocalDate Max(LocalDate a, LocalDate b) => a > b ? a : b;

    private static LocalDate Min(LocalDate a, LocalDate b) => a < b ? a : b;
}

public static class GetLeaveDeductionMonthHandler
{
    public static async Task<Result<LeaveDeductionMonthResult>> Handle(
        GetLeaveDeductionMonthQuery query,
        IReadRepository<PayrollSettings> settings,
        IReadRepository<LeaveDeductionMonth> months,
        IReadRepository<LeaveDeductionLine> lines,
        IReadRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        IReadRepository<EmployeeSalaryHistory> salaries,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        if (!LeaveDeductionMonths.CanSee(query.Caller))
        {
            return new Result<LeaveDeductionMonthResult>.Error(ResultErrors.Forbidden, "Only an Owner can see Potongan Cuti.");
        }

        var month = LeaveDeductionMonth.MonthOf(
            query.Month is { } given ? LocalDate.FromDateOnly(given) : DisplayZone.Today(clock));
        var config = await settings.GetByIdAsync(PayrollSettings.SingletonId, ct);
        if (config is null)
        {
            return new Result<LeaveDeductionMonthResult>.NotFound("Payroll settings are missing; run migrations.");
        }

        return new Result<LeaveDeductionMonthResult>.Success(await LeaveDeductionMonths.BuildAsync(
            month, config, months, lines, leaveRequests, employees, salaries, policy, clock, ct));
    }
}

/// <summary>
/// Owner-only and irreversible. Stamps every leave day of the month free or cut, and only then writes
/// the closed marker, so a failure halfway leaves the month open and pressing again is safe.
/// </summary>
public static class CloseLeaveDeductionMonthHandler
{
    public static async Task<Result<LeaveDeductionMonthResult>> Handle(
        CloseLeaveDeductionMonthCommand command,
        IReadRepository<PayrollSettings> settings,
        IRepository<LeaveDeductionMonth> months,
        IReadRepository<LeaveDeductionMonth> monthsRead,
        IRepository<LeaveDeductionLine> lines,
        IReadRepository<LeaveDeductionLine> linesRead,
        IReadRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        IReadRepository<EmployeeSalaryHistory> salaries,
        AttendanceDayPolicy policy,
        IClock clock,
        IPayrollLock payrollLock,
        CancellationToken ct)
    {
        if (!LeaveDeductionMonths.CanSee(command.Caller))
        {
            return new Result<LeaveDeductionMonthResult>.Error(ResultErrors.Forbidden, "Only an Owner can close a month.");
        }

        // Blocks leave decisions and holiday changes until this month is stamped and marked, and waits
        // for any already running, so nothing lands in the month between pricing it and closing it.
        // It also serialises two closes: the second one finds the month closed.
        await payrollLock.AcquireExclusiveAsync(ct);

        var month = LocalDate.FromDateOnly(command.Month);
        if (!LeaveDeductionMonths.IsValidMonth(month))
        {
            return new Result<LeaveDeductionMonthResult>.Error("payroll.month", "A month starts on the 1st.");
        }

        var config = await settings.GetByIdAsync(PayrollSettings.SingletonId, ct);
        if (config is null)
        {
            return new Result<LeaveDeductionMonthResult>.NotFound("Payroll settings are missing; run migrations.");
        }

        if (month < config.FirstMonth)
        {
            return new Result<LeaveDeductionMonthResult>.Error(
                "payroll.before_first_month", "Months before launch hold no leave deductions and cannot be closed.");
        }

        var closedMonths = await LeaveDeductionEngine.ClosedMonthsAsync(monthsRead, ct);
        if (closedMonths.Contains(month))
        {
            return new Result<LeaveDeductionMonthResult>.Error("payroll.already_closed", "This month is already closed.");
        }

        var end = LeaveDeductionMonth.EndOf(month);
        var today = DisplayZone.Today(clock);
        if (today <= end)
        {
            return new Result<LeaveDeductionMonthResult>.Error("payroll.not_ended", "A month can only be closed after it has ended.");
        }

        if (month != config.FirstMonth && !closedMonths.Contains(month.PlusMonths(-1)))
        {
            return new Result<LeaveDeductionMonthResult>.Error(
                "payroll.close_in_order", "An earlier month is still open; close it first.");
        }

        if ((await LeaveDeductionMonths.PendingAsync(month, end, leaveRequests, policy, ct)).Count > 0)
        {
            return new Result<LeaveDeductionMonthResult>.Error(
                "payroll.pending_leave", "Leave in this month is still waiting for a decision; decide it first.");
        }

        var priced = await LeaveDeductionEngine.PriceOpenMonthAsync(
            month, config, leaveRequests, employees, salaries, linesRead, policy, today, ct);

        // A failed earlier attempt may have left lines without the marker; start clean.
        await lines.DeleteRangeAsync(await linesRead.ListAsync(new DeductionLinesOfMonthSpec(month, tracked: true), ct), ct);
        await lines.AddRangeAsync(
            priced.Select(p => new LeaveDeductionLine(
                month, p.Employee.Id.Value, p.Day.RequestId, p.Day.Date, p.Day.Type,
                p.Day.FreeDays, p.Day.CutDays, p.Salary, p.Rate)),
            ct);
        await months.AddAsync(
            new LeaveDeductionMonth(month, command.Caller.UserId, command.Caller.Name, clock.GetCurrentInstant()), ct);

        return new Result<LeaveDeductionMonthResult>.Success(await LeaveDeductionMonths.BuildAsync(
            month, config, monthsRead, linesRead, leaveRequests, employees, salaries, policy, clock, ct));
    }
}

public static class SetPayrollDivisorHandler
{
    public static async Task<Result<PayrollSettingsResult>> Handle(
        SetPayrollDivisorCommand command,
        IRepository<PayrollSettings> settings,
        ILogger<SetPayrollDivisorCommand> logger,
        CancellationToken ct)
    {
        if (!LeaveDeductionMonths.CanSee(command.Caller))
        {
            return new Result<PayrollSettingsResult>.Error(ResultErrors.Forbidden, "Only an Owner can change the divisor.");
        }

        var config = await settings.GetByIdAsync(PayrollSettings.SingletonId, ct);
        if (config is null)
        {
            return new Result<PayrollSettingsResult>.NotFound("Payroll settings are missing; run migrations.");
        }

        var previous = config.Divisor;
        try
        {
            config.SetDivisor(command.Divisor);
        }
        catch (DomainException ex)
        {
            return new Result<PayrollSettingsResult>.Error(ex.Code ?? "payroll.validation", ex.Message);
        }

        await settings.UpdateAsync(config, ct);

        // It reprices every open month, so who moved it and from what is kept in the log.
        logger.LogInformation(
            "Payroll divisor changed from {PreviousDivisor} to {Divisor} by {UserId} ({UserName})",
            previous, config.Divisor, command.Caller.UserId, command.Caller.Name);

        return new Result<PayrollSettingsResult>.Success(new PayrollSettingsResult(config.Divisor, config.FirstMonth.ToDateOnly()));
    }
}
