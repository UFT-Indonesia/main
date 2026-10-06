using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Overtime.Common;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Erp.UseCases.Overtime.ListOvertimeAssignments;

public sealed record ListOvertimeAssignmentsQuery(
    int Page, int PageSize, DateOnly? From, DateOnly? To, Guid? EmployeeId, string? Status, Caller Caller);

public sealed record ListOvertimeAssignmentsResult(
    IReadOnlyList<OvertimeAssignmentResult> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// Scoped server-side by <see cref="OvertimeListSpec"/>: Staff their own, a Manager their own and
/// their Staff's, an Owner everyone's. Pay is stripped per row for whoever may not see it.
/// </summary>
public static class ListOvertimeAssignmentsHandler
{
    private const int MaxPageSize = 100;

    public static async Task<Result<ListOvertimeAssignmentsResult>> Handle(
        ListOvertimeAssignmentsQuery query,
        IReadRepository<OvertimeAssignment> assignments,
        IReadRepository<AttendanceLog> logs,
        AttendanceDayPolicy policy,
        IOptions<OvertimeOptions> options,
        CancellationToken ct)
    {
        OvertimeStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<OvertimeStatus>(query.Status, ignoreCase: true, out var parsed))
            {
                return new Result<ListOvertimeAssignmentsResult>.Error("overtime.status", "Unknown overtime status.");
            }

            status = parsed;
        }

        var page = Math.Max(1, query.Page);
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, MaxPageSize);
        LocalDate? from = query.From is { } f ? LocalDate.FromDateOnly(f) : null;
        LocalDate? to = query.To is { } t ? LocalDate.FromDateOnly(t) : null;
        EmployeeId? employeeId = query.EmployeeId is { } e ? new EmployeeId(e) : null;

        var total = await assignments.CountAsync(
            new OvertimeListSpec(query.Caller, from, to, employeeId, status, null, null), ct);
        var items = await assignments.ListAsync(
            new OvertimeListSpec(query.Caller, from, to, employeeId, status, page, pageSize), ct);

        var punches = await OvertimeEvaluator.LoadPunchesAsync(items, logs, policy, ct);
        return new Result<ListOvertimeAssignmentsResult>.Success(new ListOvertimeAssignmentsResult(
            items.Select(a => OvertimeMapper.ToResult(
                a, a.Employee!, OvertimeEvaluator.Evaluate(a, punches, policy, options.Value.Tiers), query.Caller)).ToList(),
            page, pageSize, total));
    }
}
