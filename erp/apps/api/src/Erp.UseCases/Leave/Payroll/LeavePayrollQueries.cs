using System.Text.Json;
using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.ListLeaveRequests;
using Erp.UseCases.Payroll;
using Erp.UseCases.Payroll.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Leave.Payroll;

/// <summary>
/// What an Owner's correction after close would do, without doing it (GSS03 follow-up Q16). <paramref name="Cancel"/>
/// previews cancelling; otherwise the edit shape is previewed.
/// </summary>
public sealed record PreviewLeaveCorrectionQuery(
    Guid LeaveRequestId, bool Cancel, DateOnly? StartDate, DateOnly? EndDate, bool HalfDay,
    HalfDayPeriod? HalfDayPeriod, int? StartHour, int? EndHour, Caller Caller);

public sealed record LeaveCorrectionPreviewLine(
    DateOnly SourceMonth, DateOnly Date, string LeaveType, decimal CutDays, decimal DailyRate, decimal Amount);

/// <summary>
/// <paramref name="TargetMonth"/> is the open month that would take <paramref name="NetAmount"/>; null when
/// nothing moves money. Over-quota days are days only, all months.
/// </summary>
public sealed record LeaveCorrectionPreviewResult(
    DateOnly ClosedMonth, decimal OverQuotaDaysBefore, decimal OverQuotaDaysAfter, DateOnly? TargetMonth,
    IReadOnlyList<LeaveCorrectionPreviewLine> Lines, decimal NetAmount);

/// <summary>Ended, unclosed months whose pending leave the caller could decide (follow-up Q7B).</summary>
public sealed record GetLeaveCloseBlockersQuery(Caller Caller);

public sealed record LeaveCloseBlockerResult(DateOnly Month, int Count);

public sealed record GetLeaveRequestQuery(Guid LeaveRequestId, Caller Caller);

internal sealed class LeaveRequestByIdReadSpec : SingleResultSpecification<LeaveRequest>
{
    public LeaveRequestByIdReadSpec(LeaveRequestId id)
    {
        Query.Where(request => request.Id == id);
        Query.AsNoTracking();
    }
}

public static class PreviewLeaveCorrectionHandler
{
    public static async Task<Result<LeaveCorrectionPreviewResult>> Handle(
        PreviewLeaveCorrectionQuery query,
        IReadRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        AttendanceDayPolicy policy,
        IClock clock,
        ClosedMonthLedger ledger,
        CancellationToken ct)
    {
        if (query.Caller.Role != EmployeeRole.Owner)
        {
            return new Result<LeaveCorrectionPreviewResult>.Error(
                ResultErrors.Forbidden, "Only the Owner can correct leave after its payroll month closed.");
        }

        // Read untracked: the edit below is applied in memory only and must never be saved.
        var request = await leaveRequests.FirstOrDefaultAsync(
            new LeaveRequestByIdReadSpec(new LeaveRequestId(query.LeaveRequestId)), ct);
        if (request is null)
        {
            return new Result<LeaveCorrectionPreviewResult>.NotFound("Leave request was not found.");
        }

        var subject = await employees.GetByIdAsync(request.EmployeeId, ct);
        if (subject is null)
        {
            return new Result<LeaveCorrectionPreviewResult>.NotFound("The employee this request belongs to was not found.");
        }

        var today = DisplayZone.Today(clock);
        var now = clock.GetCurrentInstant();
        var closedBefore = await ledger.FirstClosedMonthAsync(request.StartDate, request.EndDate, ct);
        LocalDate? closedAfter = query.Cancel || query.StartDate is null || query.EndDate is null
            ? null
            : await ledger.FirstClosedMonthAsync(
                LocalDate.FromDateOnly(query.StartDate.Value), LocalDate.FromDateOnly(query.EndDate.Value), ct);
        var closedMonth = closedBefore is { } b && closedAfter is { } a ? (b < a ? b : a) : closedBefore ?? closedAfter;
        if (closedMonth is null)
        {
            return new Result<LeaveCorrectionPreviewResult>.Error(
                "leave.not_correction", "This leave has no day in a closed payroll month.");
        }

        var before = await ledger.PlanAsync(subject, request, today, tracked: false, ct);

        try
        {
            if (query.Cancel)
            {
                request.Cancel(query.Caller.UserId, query.Caller.Name, now, null, LeaveCancellationReason.RecalledForWork);
            }
            else
            {
                var wasApproved = request.Status == LeaveRequestStatus.Approved;
                request.Edit(
                    LocalDate.FromDateOnly(query.StartDate!.Value), LocalDate.FromDateOnly(query.EndDate!.Value),
                    query.HalfDay, query.HalfDayPeriod, query.StartHour, query.EndHour,
                    query.Caller.UserId, query.Caller.Name, now, policy);

                // Same as the real edit: an Owner editing a Pending request approves it.
                if (!wasApproved)
                {
                    request.Approve(query.Caller.UserId, query.Caller.Name, now);
                }
            }
        }
        catch (DomainException ex)
        {
            return new Result<LeaveCorrectionPreviewResult>.Error(ex.Code ?? "leave.validation", ex.Message);
        }

        var after = await ledger.PlanAsync(subject, request, today, tracked: false, ct);

        return new Result<LeaveCorrectionPreviewResult>.Success(new LeaveCorrectionPreviewResult(
            closedMonth.Value.ToDateOnly(),
            before.OverQuotaDaysAfter,
            after.OverQuotaDaysAfter,
            after.TargetMonth?.ToDateOnly(),
            after.Money.Select(m => new LeaveCorrectionPreviewLine(
                    m.SourceMonth.ToDateOnly(), m.Date.ToDateOnly(), m.Type.ToString(), m.CutDays, m.DailyRate, m.Amount))
                .ToList(),
            after.NetAmount));
    }
}

/// <summary>
/// For the Leave page banner: in every month that has ended but isn't closed, how many Pending requests
/// the caller could decide right now. Days only; a Manager counts their own Staff's only.
/// </summary>
public static class GetLeaveCloseBlockersHandler
{
    public static async Task<Result<IReadOnlyList<LeaveCloseBlockerResult>>> Handle(
        GetLeaveCloseBlockersQuery query,
        IReadRepository<PayrollSettings> settings,
        IReadRepository<LeaveDeductionMonth> months,
        IReadRepository<LeaveRequest> leaveRequests,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        if (query.Caller.Role == EmployeeRole.Staff)
        {
            return new Result<IReadOnlyList<LeaveCloseBlockerResult>>.Success([]);
        }

        var config = await settings.GetByIdAsync(PayrollSettings.SingletonId, ct);
        if (config is null)
        {
            return new Result<IReadOnlyList<LeaveCloseBlockerResult>>.Success([]);
        }

        var closed = (await LeaveDeductionEngine.ClosedMonthsAsync(months, ct)).ToList();
        var firstOpen = LeaveDeductionMonths.FirstOpenMonth(closed, config.FirstMonth);
        var lastEnded = LeaveDeductionMonth.MonthOf(DisplayZone.Today(clock)).PlusMonths(-1);
        if (firstOpen > lastEnded)
        {
            return new Result<IReadOnlyList<LeaveCloseBlockerResult>>.Success([]);
        }

        var pending = (await LeaveDeductionMonths.PendingRequestsAsync(
                firstOpen, LeaveDeductionMonth.EndOf(lastEnded), leaveRequests, policy, ct))
            .Where(r => LeaveRules.CanDecideFor(query.Caller, r.Employee!)
                        && !LeaveRules.IsRequester(query.Caller, r.RequestedByUserId))
            .ToList();

        var result = new List<LeaveCloseBlockerResult>();
        for (var month = firstOpen; month <= lastEnded; month = month.PlusMonths(1))
        {
            var end = LeaveDeductionMonth.EndOf(month);
            var count = pending.Count(r => r.StartDate <= end && month <= r.EndDate
                && LeaveRequest.Workdays(r.StartDate < month ? month : r.StartDate, r.EndDate > end ? end : r.EndDate, policy).Any());
            if (count > 0)
            {
                result.Add(new LeaveCloseBlockerResult(month.ToDateOnly(), count));
            }
        }

        return new Result<IReadOnlyList<LeaveCloseBlockerResult>>.Success(result);
    }
}

/// <summary>
/// One request, exactly as the list shows it to this caller — same visibility, permissions and payroll
/// state — by running the list with an id filter. Lets Potongan Cuti open the Decide dialog (follow-up Q7A).
/// </summary>
public static class GetLeaveRequestHandler
{
    public static async Task<Result<LeaveRequestResult>> Handle(GetLeaveRequestQuery query, IMessageBus bus, CancellationToken ct)
    {
        var filter = new FilterRow("id", FilterOps.In, JsonSerializer.SerializeToElement(new[] { query.LeaveRequestId }));
        var list = await bus.InvokeAsync<Result<ListLeaveRequestsResult>>(
            new ListLeaveRequestsQuery(1, 1, [filter], query.Caller), ct);

        return list switch
        {
            Result<ListLeaveRequestsResult>.Success { Value.Items: [var item, ..] } =>
                new Result<LeaveRequestResult>.Success(item),
            Result<ListLeaveRequestsResult>.Error e => new Result<LeaveRequestResult>.Error(e.Code, e.Message),
            _ => new Result<LeaveRequestResult>.NotFound("Leave request was not found."),
        };
    }
}
