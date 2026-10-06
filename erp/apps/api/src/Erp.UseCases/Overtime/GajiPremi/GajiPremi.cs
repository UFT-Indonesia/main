using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Overtime.Common;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Erp.UseCases.Overtime.GajiPremi;

/// <summary>Null <paramref name="PeriodStart"/> means the period holding today.</summary>
public sealed record GetGajiPremiPeriodQuery(DateOnly? PeriodStart, Caller Caller);

public sealed record GetMyGajiPremiQuery(Caller Caller);

public sealed record CloseGajiPremiPeriodCommand(DateOnly PeriodStart, Caller Caller);

public sealed record GajiPremiEmployeeRow(
    Guid EmployeeId, string FullName, int Days1, int Hours1, int Days2, int Hours2,
    decimal OvertimeAmount, decimal RapelAmount, decimal Total);

/// <summary>One period for the Owner. <paramref name="Rapel"/> is what this payout carries; <paramref name="PendingRapel"/> every undecided claim, whatever period it is about.</summary>
public sealed record GajiPremiPeriodResult(
    DateOnly Start, DateOnly End, DateOnly PayoutDate, bool Closed, DateTimeOffset? ClosedAtUtc, string? ClosedByName,
    bool CanClose, decimal Total, IReadOnlyList<GajiPremiEmployeeRow> Rows,
    IReadOnlyList<RapelResult> Rapel, IReadOnlyList<RapelResult> PendingRapel);

/// <summary>
/// One row of "Gaji Premi Saya". <paramref name="Estimate"/> marks the open period, whose
/// figures move until close. <paramref name="RapelClaims"/> are claims about this period's work
/// (any status); <paramref name="RapelPaid"/> are the lines this period's payout carries.
/// </summary>
public sealed record MyGajiPremiRow(
    DateOnly Start, DateOnly End, DateOnly PayoutDate, bool Closed, bool Estimate, int Days1, int Hours1, int Days2,
    int Hours2, decimal OvertimeAmount, decimal RapelAmount, decimal Total, bool CanRequestRapel,
    IReadOnlyList<RapelResult> RapelClaims, IReadOnlyList<RapelResult> RapelPaid);

public static class GajiPremiRules
{
    public static bool IsValidStart(LocalDate start) => start == GajiPremiPeriod.StartOf(start);

    /// <summary>The first period not yet closed — where a rapel approved now gets paid. Closing is in order, so it follows the latest closed one.</summary>
    internal static async Task<LocalDate> NextPayoutStartAsync(IReadRepository<GajiPremiPeriod> periods, CancellationToken ct) =>
        (await periods.ListAsync(new ClosedPeriodsSpec(), ct)) is { Count: > 0 } closed
            ? closed[0].StartDate.PlusMonths(2)
            : throw new DomainException("rapel.no_closed_period", "No Gaji Premi period has been closed yet.");
}

internal static class GajiPremiSummary
{
    /// <summary>Approved days and counted hours, split by the period's first and second month.</summary>
    internal static (int Days1, int Hours1, int Days2, int Hours2) Split(
        LocalDate start, IEnumerable<(OvertimeAssignment A, OvertimeEvaluation E)> items)
    {
        var approved = items.Where(i => i.A.Status == OvertimeStatus.Approved).ToList();
        var first = approved.Where(i => i.A.Date.Month == start.Month).ToList();
        var second = approved.Except(first).ToList();
        return (first.Count, first.Sum(i => i.E.Hours), second.Count, second.Sum(i => i.E.Hours));
    }
}

public static class GetGajiPremiPeriodHandler
{
    public static async Task<Result<GajiPremiPeriodResult>> Handle(
        GetGajiPremiPeriodQuery query,
        IReadRepository<OvertimeAssignment> assignments,
        IReadRepository<GajiPremiPeriod> periods,
        IReadRepository<Rapel> rapels,
        IReadRepository<AttendanceLog> logs,
        AttendanceDayPolicy policy,
        IOptions<OvertimeOptions> options,
        IClock clock,
        CancellationToken ct)
    {
        if (query.Caller.Role != EmployeeRole.Owner)
        {
            return new Result<GajiPremiPeriodResult>.Error(ResultErrors.Forbidden, "Only an Owner can see Gaji Premi.");
        }

        var start = query.PeriodStart is { } given ? LocalDate.FromDateOnly(given) : GajiPremiPeriod.StartOf(DisplayZone.Today(clock));
        if (!GajiPremiRules.IsValidStart(start))
        {
            return new Result<GajiPremiPeriodResult>.Error(
                "gaji_premi.period", "A period starts on the 1st of an odd month (Jan, Mar, May, Jul, Sep, Nov).");
        }

        return new Result<GajiPremiPeriodResult>.Success(
            await BuildAsync(start, assignments, periods, rapels, logs, policy, options.Value.Tiers, clock, ct));
    }

    internal static async Task<GajiPremiPeriodResult> BuildAsync(
        LocalDate start, IReadRepository<OvertimeAssignment> assignments, IReadRepository<GajiPremiPeriod> periods,
        IReadRepository<Rapel> rapels, IReadRepository<AttendanceLog> logs, AttendanceDayPolicy policy,
        IEnumerable<OvertimeTier> tiers, IClock clock, CancellationToken ct)
    {
        var end = GajiPremiPeriod.EndOf(start);
        var closed = await periods.FirstOrDefaultAsync(new ClosedPeriodByStartSpec(start), ct);

        var inPeriod = await assignments.ListAsync(new PeriodOvertimeSpec(start, end), ct);
        var punches = await OvertimeEvaluator.LoadPunchesAsync(inPeriod, logs, policy, ct);
        var evaluated = inPeriod.Select(a => (A: a, E: OvertimeEvaluator.Evaluate(a, punches, policy, tiers))).ToList();

        var paid = await rapels.ListAsync(new RapelForPayoutSpec(start), ct);
        var pending = await rapels.ListAsync(
            new RapelListSpec(new Caller(Guid.Empty, EmployeeRole.Owner, null, "Owner"), status: OvertimeRequestStatus.Pending), ct);

        var employees = evaluated.Select(i => i.A.Employee!).Concat(paid.Select(r => r.Employee!)).DistinctBy(e => e.Id);
        var rows = employees.Select(e =>
            {
                var mine = evaluated.Where(i => i.A.EmployeeId == e.Id).ToList();
                var (d1, h1, d2, h2) = GajiPremiSummary.Split(start, mine);
                var overtime = mine.Sum(i => i.E.Amount);
                var rapel = paid.Where(r => r.EmployeeId == e.Id).Sum(r => r.Amount ?? 0m);
                return new GajiPremiEmployeeRow(e.Id.Value, e.FullName, d1, h1, d2, h2, overtime, rapel, overtime + rapel);
            })
            .OrderBy(r => r.FullName)
            .ToList();

        var canClose = closed is null && DisplayZone.Today(clock) > end
            && !await assignments.AnyAsync(new UnfrozenOvertimeBeforeSpec(start), ct);

        return new GajiPremiPeriodResult(
            start.ToDateOnly(), end.ToDateOnly(), GajiPremiPeriod.PayoutDate(start).ToDateOnly(), closed is not null,
            closed?.ClosedAtUtc.ToDateTimeOffset(), closed?.ClosedByName, canClose, rows.Sum(r => r.Total), rows,
            paid.Select(r => OvertimeMapper.ToResult(r, r.Employee!)).ToList(),
            pending.Select(r => OvertimeMapper.ToResult(r, r.Employee!)).ToList());
    }
}

/// <summary>An employee's own table of OT disbursements, one row per period they have anything in, newest first. Self-only.</summary>
public static class GetMyGajiPremiHandler
{
    public static async Task<Result<IReadOnlyList<MyGajiPremiRow>>> Handle(
        GetMyGajiPremiQuery query,
        IReadRepository<OvertimeAssignment> assignments,
        IReadRepository<GajiPremiPeriod> periods,
        IReadRepository<Rapel> rapels,
        IReadRepository<AttendanceLog> logs,
        AttendanceDayPolicy policy,
        IOptions<OvertimeOptions> options,
        IClock clock,
        CancellationToken ct)
    {
        if (query.Caller.EmployeeId is not { } me)
        {
            return new Result<IReadOnlyList<MyGajiPremiRow>>.Success([]);
        }

        var farPast = new LocalDate(2000, 1, 1);
        var farFuture = new LocalDate(2100, 1, 1);
        var mine = await assignments.ListAsync(new PeriodOvertimeSpec(farPast, farFuture, me), ct);
        var punches = await OvertimeEvaluator.LoadPunchesAsync(mine, logs, policy, ct);
        var evaluated = mine.Select(a => (A: a, E: OvertimeEvaluator.Evaluate(a, punches, policy, options.Value.Tiers))).ToList();

        var myRapel = await rapels.ListAsync(new RapelListSpec(query.Caller, me), ct);
        var closedStarts = (await periods.ListAsync(new ClosedPeriodsSpec(), ct)).Select(p => p.StartDate).ToHashSet();

        var starts = evaluated.Select(i => GajiPremiPeriod.StartOf(i.A.Date))
            .Concat(myRapel.Select(r => GajiPremiPeriod.StartOf(r.WorkDate)))
            .Concat(myRapel.Where(r => r.PayoutPeriodStart is not null).Select(r => r.PayoutPeriodStart!.Value))
            .Append(GajiPremiPeriod.StartOf(DisplayZone.Today(clock)))
            .Distinct()
            .OrderByDescending(s => s);

        var rows = starts.Select(start =>
        {
            var end = GajiPremiPeriod.EndOf(start);
            var inPeriod = evaluated.Where(i => i.A.Date >= start && i.A.Date <= end).ToList();
            var (d1, h1, d2, h2) = GajiPremiSummary.Split(start, inPeriod);
            var overtime = inPeriod.Sum(i => i.E.Amount);
            var paidLines = myRapel.Where(r => r.Status == OvertimeRequestStatus.Approved && r.PayoutPeriodStart == start).ToList();
            var rapelAmount = paidLines.Sum(r => r.Amount ?? 0m);
            var isClosed = closedStarts.Contains(start);
            return new MyGajiPremiRow(
                start.ToDateOnly(), end.ToDateOnly(), GajiPremiPeriod.PayoutDate(start).ToDateOnly(), isClosed, !isClosed,
                d1, h1, d2, h2, overtime, rapelAmount, overtime + rapelAmount, CanRequestRapel: isClosed,
                myRapel.Where(r => r.WorkDate >= start && r.WorkDate <= end).Select(r => OvertimeMapper.ToResult(r, r.Employee!)).ToList(),
                paidLines.Select(r => OvertimeMapper.ToResult(r, r.Employee!)).ToList());
        }).ToList();

        return new Result<IReadOnlyList<MyGajiPremiRow>>.Success(rows);
    }
}

/// <summary>
/// Owner-only and irreversible. Expires what is still undecided, freezes every assignment's
/// counted hours and pay, and only then writes the closed marker — so a failure halfway leaves
/// the period open and the same action safe to press again.
/// </summary>
public static class CloseGajiPremiPeriodHandler
{
    public static async Task<Result<GajiPremiPeriodResult>> Handle(
        CloseGajiPremiPeriodCommand command,
        IRepository<OvertimeAssignment> assignments,
        IReadRepository<OvertimeAssignment> assignmentsRead,
        IRepository<OvertimeCorrectionRequest> corrections,
        IRepository<GajiPremiPeriod> periods,
        IReadRepository<GajiPremiPeriod> periodsRead,
        IReadRepository<Rapel> rapels,
        IReadRepository<AttendanceLog> logs,
        AttendanceDayPolicy policy,
        IOptions<OvertimeOptions> options,
        IClock clock,
        CancellationToken ct)
    {
        if (command.Caller.Role != EmployeeRole.Owner)
        {
            return new Result<GajiPremiPeriodResult>.Error(ResultErrors.Forbidden, "Only an Owner can close a period.");
        }

        var start = LocalDate.FromDateOnly(command.PeriodStart);
        var end = GajiPremiPeriod.EndOf(start);
        if (!GajiPremiRules.IsValidStart(start))
        {
            return new Result<GajiPremiPeriodResult>.Error(
                "gaji_premi.period", "A period starts on the 1st of an odd month (Jan, Mar, May, Jul, Sep, Nov).");
        }

        if (await periodsRead.AnyAsync(new ClosedPeriodByStartSpec(start), ct))
        {
            return new Result<GajiPremiPeriodResult>.Error("gaji_premi.already_closed", "This period is already closed.");
        }

        if (DisplayZone.Today(clock) <= end)
        {
            return new Result<GajiPremiPeriodResult>.Error("gaji_premi.not_ended", "A period can only be closed after it has ended.");
        }

        if (await assignmentsRead.AnyAsync(new UnfrozenOvertimeBeforeSpec(start), ct))
        {
            return new Result<GajiPremiPeriodResult>.Error(
                "gaji_premi.close_in_order", "An earlier period still has open overtime; close it first.");
        }

        var now = clock.GetCurrentInstant();
        var inPeriod = await assignments.ListAsync(new PeriodOvertimeSpec(start, end, tracked: true), ct);
        var punches = await OvertimeEvaluator.LoadPunchesAsync(inPeriod, logs, policy, ct);

        foreach (var a in inPeriod.Where(a => !a.IsFrozen))
        {
            a.Expire();
            var evaluation = OvertimeEvaluator.Evaluate(a, punches, policy, options.Value.Tiers);
            a.Freeze(evaluation.Hours, evaluation.Amount, now);
            await assignments.UpdateAsync(a, ct);
        }

        foreach (var correction in await corrections.ListAsync(new PendingCorrectionsSpec(from: start, to: end), ct))
        {
            correction.Expire();
            await corrections.UpdateAsync(correction, ct);
        }

        await periods.AddAsync(GajiPremiPeriod.Close(start, command.Caller.UserId, command.Caller.Name, now), ct);

        return new Result<GajiPremiPeriodResult>.Success(await GetGajiPremiPeriodHandler.BuildAsync(
            start, assignmentsRead, periodsRead, rapels, logs, policy, options.Value.Tiers, clock, ct));
    }
}
