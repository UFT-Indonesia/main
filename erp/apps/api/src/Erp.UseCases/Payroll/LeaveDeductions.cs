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

public sealed record GetPayrollDivisorHistoryQuery(Caller Caller);

/// <summary>Positive deducts, negative refunds. Whole rupiah.</summary>
public sealed record AddLeaveDeductionAdjustmentCommand(
    DateOnly Month, Guid EmployeeId, decimal Amount, string Reason, Caller Caller);

public sealed record DeleteLeaveDeductionAdjustmentCommand(Guid AdjustmentId, Caller Caller);

/// <summary>
/// One leave day of one request: how much of it is free and how much is cut, and what that is worth.
/// On a closed month the markers say what happened to it after close (GSS03 follow-up Q17).
/// </summary>
public sealed record LeaveDeductionDayResult(
    DateOnly Date, string LeaveType, Guid LeaveRequestId, DateOnly RequestStart, DateOnly RequestEnd,
    decimal FreeDays, decimal CutDays, decimal Salary, decimal DailyRate, decimal Amount,
    bool PaidAtClose = true, DateOnly? LateTargetMonth = null,
    DateTimeOffset? SupersededAtUtc = null, string? SupersededByName = null, DateOnly? SupersededTargetMonth = null);

/// <summary>Money moved into this month by a correction of a closed month (follow-up Q9). Signed.</summary>
public sealed record LateCorrectionResult(
    Guid Id, DateOnly SourceMonth, DateOnly Date, string LeaveType, Guid LeaveRequestId,
    decimal CutDays, decimal DailyRate, decimal Amount, string Reason, string ByName, DateTimeOffset AtUtc);

/// <summary>An Owner's manual line (follow-up Q12). Signed whole rupiah.</summary>
public sealed record LeaveDeductionAdjustmentResult(
    Guid Id, Guid EmployeeId, decimal Amount, string Reason, string ByName, DateTimeOffset AtUtc);

/// <summary>
/// One employee's month. <paramref name="Total"/> is every exact amount — cut days, late corrections and
/// adjustments — summed and rounded toward zero to Rp 1.000 once. Negative means a refund.
/// </summary>
public sealed record LeaveDeductionEmployeeRow(
    Guid EmployeeId, string FullName, IReadOnlyDictionary<string, decimal> CutDaysByType, decimal CutDays,
    decimal Total, IReadOnlyList<LeaveDeductionDayResult> Days,
    IReadOnlyList<LateCorrectionResult> Corrections, IReadOnlyList<LeaveDeductionAdjustmentResult> Adjustments);

public sealed record PendingLeaveItem(
    Guid LeaveRequestId, Guid EmployeeId, string FullName, string LeaveType, DateOnly Start, DateOnly End);

/// <summary>
/// Why a month can't be closed yet (follow-up Q7C): <c>pending</c> (with <paramref name="Count"/>),
/// <c>earlier_open</c> (with the <paramref name="Month"/> to close first), <c>not_ended</c>, <c>before_launch</c>.
/// </summary>
public sealed record CloseBlocker(string Code, int? Count = null, DateOnly? Month = null);

public sealed record DivisorChangeResult(int OldDivisor, int NewDivisor, string ByName, DateTimeOffset AtUtc);

public sealed record LeaveDeductionMonthResult(
    DateOnly Month, bool Closed, DateTimeOffset? ClosedAtUtc, string? ClosedByName, bool CanClose,
    IReadOnlyList<CloseBlocker> CloseBlockers, int Divisor, DateOnly FirstMonth,
    decimal Total, decimal CutsTotal, decimal RefundsTotal,
    IReadOnlyList<LeaveDeductionEmployeeRow> Rows, IReadOnlyList<PendingLeaveItem> Pending,
    DivisorChangeResult? LastDivisorChange);

public sealed record PayrollSettingsResult(int Divisor, DateOnly FirstMonth);

/// <summary>Everything the Potongan Cuti month view reads.</summary>
public sealed record LeaveDeductionReaders(
    IReadRepository<LeaveDeductionMonth> Months,
    IReadRepository<LeaveDeductionLine> Lines,
    IReadRepository<LeaveDeductionCorrection> Corrections,
    IReadRepository<LeaveDeductionAdjustment> Adjustments,
    IReadRepository<PayrollSettingsChange> DivisorChanges,
    IReadRepository<LeaveRequest> LeaveRequests,
    IReadRepository<Employee> Employees,
    IReadRepository<EmployeeSalaryHistory> Salaries);

internal static class LeaveDeductionMonths
{
    internal static bool IsValidMonth(LocalDate month) => month == LeaveDeductionMonth.MonthOf(month);

    /// <summary>The page and every amount on it are Owner-only.</summary>
    internal static bool CanSee(Caller caller) => caller.Role == EmployeeRole.Owner;

    private sealed record DayLine(
        Guid EmployeeId, LeaveType Type, LocalDate Date, Guid RequestId,
        decimal Free, decimal Cut, decimal Salary, decimal Rate,
        bool PaidAtClose, LocalDate? LateTarget, Instant? SupersededAt, string? SupersededBy, LocalDate? SupersededTarget)
    {
        /// <summary>What counts in this month's total: on a closed month only what was paid at close.</summary>
        public decimal CountedAmount => PaidAtClose ? Cut * Rate : 0m;
    }

    /// <summary>The first open month: the one after the latest closed, never before launch.</summary>
    internal static LocalDate FirstOpenMonth(IReadOnlyCollection<LocalDate> closed, LocalDate firstMonth)
    {
        if (closed.Count == 0)
        {
            return firstMonth;
        }

        var next = closed.Max().PlusMonths(1);
        return next < firstMonth ? firstMonth : next;
    }

    internal static async Task<LeaveDeductionMonthResult> BuildAsync(
        LocalDate month,
        PayrollSettings settings,
        LeaveDeductionReaders read,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        var end = LeaveDeductionMonth.EndOf(month);
        var today = DisplayZone.Today(clock);
        var closed = await read.Months.FirstOrDefaultAsync(new DeductionMonthSpec(month), ct);

        List<DayLine> days;
        var names = new Dictionary<Guid, string>();
        if (closed is not null)
        {
            days = (await read.Lines.ListAsync(new DeductionLinesOfMonthSpec(month), ct))
                .Select(l => new DayLine(
                    l.EmployeeId, l.Type, l.Date, l.LeaveRequestId, l.FreeDays, l.CutDays, l.Salary, l.DailyRate,
                    l.PaidAtClose, l.LateTargetMonth, l.SupersededAtUtc, l.SupersededByName, l.SupersededTargetMonth))
                .ToList();
        }
        else
        {
            var priced = await LeaveDeductionEngine.PriceOpenMonthAsync(
                month, settings, read.LeaveRequests, read.Employees, read.Salaries, read.Lines, policy, today, ct);
            foreach (var p in priced)
            {
                names[p.Employee.Id.Value] = p.Employee.FullName;
            }

            days = priced.Select(p => new DayLine(
                    p.Employee.Id.Value, p.Day.Type, p.Day.Date, p.Day.RequestId, p.Day.FreeDays, p.Day.CutDays,
                    p.Salary, p.Rate, true, null, null, null, null))
                .ToList();
        }

        var corrections = await read.Corrections.ListAsync(new CorrectionsTargetingMonthSpec(month), ct);
        var adjustments = await read.Adjustments.ListAsync(new AdjustmentsOfMonthSpec(month), ct);

        var missing = days.Select(d => d.EmployeeId)
            .Concat(corrections.Select(c => c.EmployeeId))
            .Concat(adjustments.Select(a => a.EmployeeId))
            .Distinct()
            .Where(id => !names.ContainsKey(id))
            .Select(id => new EmployeeId(id))
            .ToList();
        if (missing.Count > 0)
        {
            foreach (var e in await read.Employees.ListAsync(new EmployeesByIdsSpec(missing), ct))
            {
                names[e.Id.Value] = e.FullName;
            }
        }

        var requestRanges = (await read.LeaveRequests.ListAsync(
                new LeaveRequestsByIdsSpec(days.Select(d => new LeaveRequestId(d.RequestId)).Distinct().ToList()), ct))
            .ToDictionary(r => r.Id.Value, r => (r.StartDate, r.EndDate));

        var daysByEmployee = days.ToLookup(d => d.EmployeeId);
        var correctionsByEmployee = corrections.ToLookup(c => c.EmployeeId);
        var adjustmentsByEmployee = adjustments.ToLookup(a => a.EmployeeId);
        var employeeIds = days.Select(d => d.EmployeeId)
            .Concat(corrections.Select(c => c.EmployeeId))
            .Concat(adjustments.Select(a => a.EmployeeId))
            .Distinct();

        var rows = employeeIds.Select(employeeId =>
        {
            var mine = daysByEmployee[employeeId].ToList();

            // Cut days of the month itself: paid ones on a closed month, every live one on an open month.
            var counted = mine.Where(d => d.PaidAtClose && d.Cut > 0).ToList();
            var cutByType = counted
                .GroupBy(d => d.Type.ToString())
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Cut));

            var dayResults = mine.OrderBy(d => d.Date).Select(d =>
            {
                var (start, finish) = requestRanges.GetValueOrDefault(d.RequestId, (d.Date, d.Date));
                return new LeaveDeductionDayResult(
                    d.Date.ToDateOnly(), d.Type.ToString(), d.RequestId, start.ToDateOnly(), finish.ToDateOnly(),
                    d.Free, d.Cut, d.Salary, d.Rate, d.Cut * d.Rate,
                    d.PaidAtClose, d.LateTarget?.ToDateOnly(),
                    d.SupersededAt?.ToDateTimeOffset(), d.SupersededBy, d.SupersededTarget?.ToDateOnly());
            }).ToList();

            var correctionResults = correctionsByEmployee[employeeId].OrderBy(c => c.Date).Select(c =>
                new LateCorrectionResult(
                    c.Id, c.SourceMonth.ToDateOnly(), c.Date.ToDateOnly(), c.Type.ToString(), c.LeaveRequestId,
                    c.CutDays, c.DailyRate, c.Amount, c.Reason, c.ByName, c.AtUtc.ToDateTimeOffset())).ToList();
            var adjustmentResults = adjustmentsByEmployee[employeeId].OrderBy(a => a.AtUtc).Select(a =>
                new LeaveDeductionAdjustmentResult(
                    a.Id, a.EmployeeId, a.Amount, a.Reason, a.ByName, a.AtUtc.ToDateTimeOffset())).ToList();

            var total = LeaveDeductionCalculator.MonthTotal(
                mine.Select(d => d.CountedAmount)
                    .Concat(correctionResults.Select(c => c.Amount))
                    .Concat(adjustmentResults.Select(a => a.Amount)));

            return new LeaveDeductionEmployeeRow(
                employeeId, names.GetValueOrDefault(employeeId, "?"), cutByType, cutByType.Values.Sum(), total,
                dayResults, correctionResults, adjustmentResults);
        }).OrderBy(r => r.FullName).ToList();

        var pending = closed is null ? await PendingAsync(month, end, read.LeaveRequests, policy, ct) : [];

        var closedMonths = (await read.Months.ListAsync(new ClosedDeductionMonthsSpec(), ct)).Select(m => m.Month).ToList();
        var blockers = closed is not null
            ? []
            : CloseBlockersFor(month, end, today, settings.FirstMonth, closedMonths, pending.Count);

        var lastChange = (await read.DivisorChanges.ListAsync(new DivisorChangesSpec(take: 1), ct)).FirstOrDefault();

        return new LeaveDeductionMonthResult(
            month.ToDateOnly(), closed is not null, closed?.ClosedAtUtc.ToDateTimeOffset(), closed?.ClosedByName,
            CanClose: closed is null && blockers.Count == 0,
            blockers,
            closed?.Divisor ?? settings.Divisor,
            settings.FirstMonth.ToDateOnly(),
            Total: rows.Sum(r => r.Total),
            CutsTotal: rows.Where(r => r.Total > 0).Sum(r => r.Total),
            RefundsTotal: -rows.Where(r => r.Total < 0).Sum(r => r.Total),
            rows,
            pending,
            lastChange is null
                ? null
                : new DivisorChangeResult(
                    lastChange.OldDivisor, lastChange.NewDivisor, lastChange.ChangedByName,
                    lastChange.ChangedAtUtc.ToDateTimeOffset()));
    }

    /// <summary>Every reason an open month can't be closed yet, in the order the Owner would fix them.</summary>
    internal static IReadOnlyList<CloseBlocker> CloseBlockersFor(
        LocalDate month, LocalDate end, LocalDate today, LocalDate firstMonth,
        IReadOnlyCollection<LocalDate> closedMonths, int pendingCount)
    {
        if (month < firstMonth)
        {
            return [new CloseBlocker("before_launch")];
        }

        var blockers = new List<CloseBlocker>();
        if (today <= end)
        {
            blockers.Add(new CloseBlocker("not_ended", Month: month.ToDateOnly()));
        }

        var firstOpen = FirstOpenMonth(closedMonths, firstMonth);
        if (firstOpen < month)
        {
            blockers.Add(new CloseBlocker("earlier_open", Month: firstOpen.ToDateOnly()));
        }

        if (pendingCount > 0)
        {
            blockers.Add(new CloseBlocker("pending", Count: pendingCount));
        }

        return blockers;
    }

    /// <summary>Undecided leave with a workday in the month — close is blocked until each is decided (decision 7).</summary>
    internal static async Task<IReadOnlyList<PendingLeaveItem>> PendingAsync(
        LocalDate month, LocalDate end, IReadRepository<LeaveRequest> leaveRequests, AttendanceDayPolicy policy,
        CancellationToken ct) =>
        (await PendingRequestsAsync(month, end, leaveRequests, policy, ct))
            .Select(r => new PendingLeaveItem(
                r.Id.Value, r.EmployeeId.Value, r.Employee!.FullName, r.Type.ToString(),
                r.StartDate.ToDateOnly(), r.EndDate.ToDateOnly()))
            .ToList();

    internal static async Task<IReadOnlyList<LeaveRequest>> PendingRequestsAsync(
        LocalDate month, LocalDate end, IReadRepository<LeaveRequest> leaveRequests, AttendanceDayPolicy policy,
        CancellationToken ct) =>
        (await leaveRequests.ListAsync(new PendingLeaveOverlappingSpec(month, end), ct))
            .Where(r => r.Employee is { Role: not EmployeeRole.Owner }
                        && LeaveRequest.Workdays(Max(r.StartDate, month), Min(r.EndDate, end), policy).Any())
            .OrderBy(r => r.StartDate)
            .ToList();

    private static LocalDate Max(LocalDate a, LocalDate b) => a > b ? a : b;

    private static LocalDate Min(LocalDate a, LocalDate b) => a < b ? a : b;
}

public static class GetLeaveDeductionMonthHandler
{
    public static async Task<Result<LeaveDeductionMonthResult>> Handle(
        GetLeaveDeductionMonthQuery query,
        IReadRepository<PayrollSettings> settings,
        LeaveDeductionReaders read,
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

        return new Result<LeaveDeductionMonthResult>.Success(
            await LeaveDeductionMonths.BuildAsync(month, config, read, policy, clock, ct));
    }
}

/// <summary>
/// Owner-only and irreversible. Stamps every leave day of the month free or cut, records the divisor and
/// each employee's exception as they stand (follow-up Q10), and writes the closed marker.
/// </summary>
public static class CloseLeaveDeductionMonthHandler
{
    public static async Task<Result<LeaveDeductionMonthResult>> Handle(
        CloseLeaveDeductionMonthCommand command,
        IReadRepository<PayrollSettings> settings,
        IRepository<LeaveDeductionMonth> months,
        IRepository<LeaveDeductionLine> lines,
        IRepository<LeaveDeductionMonthException> monthExceptions,
        LeaveDeductionReaders read,
        AttendanceDayPolicy policy,
        IClock clock,
        IPayrollLock payrollLock,
        CancellationToken ct)
    {
        if (!LeaveDeductionMonths.CanSee(command.Caller))
        {
            return new Result<LeaveDeductionMonthResult>.Error(ResultErrors.Forbidden, "Only an Owner can close a month.");
        }

        // Blocks leave decisions, holiday changes and adjustments until this month is stamped and marked,
        // and waits for any already running, so nothing lands in the month between pricing and closing it.
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

        var closedMonths = await LeaveDeductionEngine.ClosedMonthsAsync(read.Months, ct);
        if (closedMonths.Contains(month))
        {
            return new Result<LeaveDeductionMonthResult>.Error("payroll.already_closed", "This month is already closed.");
        }

        var end = LeaveDeductionMonth.EndOf(month);
        var today = DisplayZone.Today(clock);
        var pending = await LeaveDeductionMonths.PendingAsync(month, end, read.LeaveRequests, policy, ct);
        var blockers = LeaveDeductionMonths.CloseBlockersFor(
            month, end, today, config.FirstMonth, closedMonths.ToList(), pending.Count);
        if (blockers.Count > 0)
        {
            return blockers[0].Code switch
            {
                "before_launch" => new Result<LeaveDeductionMonthResult>.Error(
                    "payroll.before_first_month", "Months before launch hold no leave deductions and cannot be closed."),
                "not_ended" => new Result<LeaveDeductionMonthResult>.Error(
                    "payroll.not_ended", "A month can only be closed after it has ended."),
                "earlier_open" => new Result<LeaveDeductionMonthResult>.Error(
                    "payroll.close_in_order", "An earlier month is still open; close it first."),
                _ => new Result<LeaveDeductionMonthResult>.Error(
                    "payroll.pending_leave", "Leave in this month is still waiting for a decision; decide it first."),
            };
        }

        var priced = await LeaveDeductionEngine.PriceOpenMonthAsync(
            month, config, read.LeaveRequests, read.Employees, read.Salaries, read.Lines, policy, today, ct);

        await lines.AddRangeAsync(
            priced.Select(p => new LeaveDeductionLine(
                month, p.Employee.Id.Value, p.Day.RequestId, p.Day.Date, p.Day.Type,
                p.Day.FreeDays, p.Day.CutDays, p.Salary, p.Rate)),
            ct);

        var snapshots = (await read.Employees.ListAsync(new NonOwnerEmployeesSpec(), ct))
            .Where(e => e.LeaveDeductionException is not null)
            .Select(e => new LeaveDeductionMonthException(
                month, e.Id.Value, e.LeaveDeductionException!.FlatAmountPerDay, e.LeaveDeductionException.Divisor))
            .ToList();
        if (snapshots.Count > 0)
        {
            await monthExceptions.AddRangeAsync(snapshots, ct);
        }

        await months.AddAsync(
            new LeaveDeductionMonth(
                month, config.Divisor, command.Caller.UserId, command.Caller.Name, clock.GetCurrentInstant()),
            ct);

        return new Result<LeaveDeductionMonthResult>.Success(
            await LeaveDeductionMonths.BuildAsync(month, config, read, policy, clock, ct));
    }
}

public static class SetPayrollDivisorHandler
{
    public static async Task<Result<PayrollSettingsResult>> Handle(
        SetPayrollDivisorCommand command,
        IRepository<PayrollSettings> settings,
        IRepository<PayrollSettingsChange> changes,
        IClock clock,
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

        if (previous != config.Divisor)
        {
            await settings.UpdateAsync(config, ct);

            // It reprices every open month, so who moved it and from what is kept, and shown on the page (Q5).
            await changes.AddAsync(
                new PayrollSettingsChange(
                    previous, config.Divisor, command.Caller.UserId, command.Caller.Name, clock.GetCurrentInstant()),
                ct);
            logger.LogInformation(
                "Payroll divisor changed from {PreviousDivisor} to {Divisor} by {UserId} ({UserName})",
                previous, config.Divisor, command.Caller.UserId, command.Caller.Name);
        }

        return new Result<PayrollSettingsResult>.Success(new PayrollSettingsResult(config.Divisor, config.FirstMonth.ToDateOnly()));
    }
}

public static class GetPayrollDivisorHistoryHandler
{
    public static async Task<Result<IReadOnlyList<DivisorChangeResult>>> Handle(
        GetPayrollDivisorHistoryQuery query,
        IReadRepository<PayrollSettingsChange> changes,
        CancellationToken ct)
    {
        if (!LeaveDeductionMonths.CanSee(query.Caller))
        {
            return new Result<IReadOnlyList<DivisorChangeResult>>.Error(
                ResultErrors.Forbidden, "Only an Owner can see the divisor history.");
        }

        var rows = await changes.ListAsync(new DivisorChangesSpec(), ct);
        return new Result<IReadOnlyList<DivisorChangeResult>>.Success(rows
            .Select(c => new DivisorChangeResult(c.OldDivisor, c.NewDivisor, c.ChangedByName, c.ChangedAtUtc.ToDateTimeOffset()))
            .ToList());
    }
}

/// <summary>
/// Owner-only manual line on an open month (follow-up Q12/Q15): any non-Owner employee who is active or
/// was terminated within the month, a non-zero whole-rupiah amount, a reason. Frozen when the month closes.
/// </summary>
public static class AddLeaveDeductionAdjustmentHandler
{
    public static async Task<Result<LeaveDeductionAdjustmentResult>> Handle(
        AddLeaveDeductionAdjustmentCommand command,
        IReadRepository<PayrollSettings> settings,
        IReadRepository<LeaveDeductionMonth> months,
        IReadRepository<Employee> employees,
        IRepository<LeaveDeductionAdjustment> adjustments,
        IClock clock,
        IPayrollLock payrollLock,
        CancellationToken ct)
    {
        if (!LeaveDeductionMonths.CanSee(command.Caller))
        {
            return new Result<LeaveDeductionAdjustmentResult>.Error(ResultErrors.Forbidden, "Only an Owner can add an adjustment.");
        }

        await payrollLock.AcquireSharedAsync(ct);

        var month = LocalDate.FromDateOnly(command.Month);
        if (!LeaveDeductionMonths.IsValidMonth(month))
        {
            return new Result<LeaveDeductionAdjustmentResult>.Error("payroll.month", "A month starts on the 1st.");
        }

        var config = await settings.GetByIdAsync(PayrollSettings.SingletonId, ct);
        if (config is null)
        {
            return new Result<LeaveDeductionAdjustmentResult>.NotFound("Payroll settings are missing; run migrations.");
        }

        if (month < config.FirstMonth)
        {
            return new Result<LeaveDeductionAdjustmentResult>.Error(
                "payroll.before_first_month", "Months before launch can't hold adjustments.");
        }

        if ((await LeaveDeductionEngine.ClosedMonthsAsync(months, ct)).Contains(month))
        {
            return new Result<LeaveDeductionAdjustmentResult>.Error("payroll.already_closed", "This month is already closed.");
        }

        var employee = await employees.GetByIdAsync(new EmployeeId(command.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<LeaveDeductionAdjustmentResult>.NotFound("Employee was not found.");
        }

        if (employee.Role == EmployeeRole.Owner)
        {
            return new Result<LeaveDeductionAdjustmentResult>.Error(
                "payroll.adjustment_owner", "An Owner's salary is never cut, so it can't be adjusted here.");
        }

        if (employee.TerminationDate is { } terminated && terminated < month)
        {
            return new Result<LeaveDeductionAdjustmentResult>.Error(
                "payroll.adjustment_terminated", $"{employee.FullName} left before this month.");
        }

        LeaveDeductionAdjustment adjustment;
        try
        {
            adjustment = new LeaveDeductionAdjustment(
                month, employee.Id.Value, command.Amount, command.Reason, command.Caller.UserId, command.Caller.Name,
                clock.GetCurrentInstant());
        }
        catch (DomainException ex)
        {
            return new Result<LeaveDeductionAdjustmentResult>.Error(ex.Code ?? "payroll.validation", ex.Message);
        }

        await adjustments.AddAsync(adjustment, ct);
        return new Result<LeaveDeductionAdjustmentResult>.Success(new LeaveDeductionAdjustmentResult(
            adjustment.Id, adjustment.EmployeeId, adjustment.Amount, adjustment.Reason, adjustment.ByName,
            adjustment.AtUtc.ToDateTimeOffset()));
    }
}

/// <summary>Owner-only, while the month is open. There is no edit: delete and add again (follow-up Q15).</summary>
public static class DeleteLeaveDeductionAdjustmentHandler
{
    public static async Task<Result<bool>> Handle(
        DeleteLeaveDeductionAdjustmentCommand command,
        IReadRepository<LeaveDeductionMonth> months,
        IRepository<LeaveDeductionAdjustment> adjustments,
        IPayrollLock payrollLock,
        CancellationToken ct)
    {
        if (!LeaveDeductionMonths.CanSee(command.Caller))
        {
            return new Result<bool>.Error(ResultErrors.Forbidden, "Only an Owner can remove an adjustment.");
        }

        await payrollLock.AcquireSharedAsync(ct);

        var adjustment = await adjustments.GetByIdAsync(command.AdjustmentId, ct);
        if (adjustment is null)
        {
            return new Result<bool>.NotFound("Adjustment was not found.");
        }

        if ((await LeaveDeductionEngine.ClosedMonthsAsync(months, ct)).Contains(adjustment.Month))
        {
            return new Result<bool>.Error("payroll.already_closed", "This month is closed; its adjustments are final.");
        }

        await adjustments.DeleteAsync(adjustment, ct);
        return new Result<bool>.Success(true);
    }
}
