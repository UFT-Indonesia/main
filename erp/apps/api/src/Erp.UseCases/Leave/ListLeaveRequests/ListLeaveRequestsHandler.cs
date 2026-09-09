using System.Linq.Expressions;
using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Leave.Common;
using NodaTime;

namespace Erp.UseCases.Leave.ListLeaveRequests;

public static class ListLeaveRequestsHandler
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static async Task<Result<ListLeaveRequestsResult>> Handle(
        ListLeaveRequestsQuery query,
        IReadRepository<LeaveRequest> leaveRequests,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(query.PageSize, MaxPageSize);

        if (!FilterApplier.TryCompile(LeaveRequestFilterFields.Fields, query.Filters, query.Caller, out var filters, out var failure))
        {
            return new Result<ListLeaveRequestsResult>.Error(failure.Code, failure.Message);
        }

        var totalCount = await leaveRequests.CountAsync(
            new LeaveRequestListCountSpec(filters, query.Caller), ct);
        var items = await leaveRequests.ListAsync(
            new LeaveRequestListSpec(page, pageSize, filters, query.Caller), ct);

        // Balances for every employee on the page, one query. Days are attributed to the year
        // they fall in rather than to the year the request started in, so a request over New Year
        // counts against both — which is what the quota check enforces.
        var today = DisplayZone.Today(clock);
        var year = today.Year;
        var employeeIds = items.Select(request => request.EmployeeId).Distinct().ToList();
        var approvedThisYear = employeeIds.Count == 0
            ? []
            : await leaveRequests.ListAsync(new ApprovedLeaveForYearSpec(employeeIds, year), ct);
        var approvedByEmployee = approvedThisYear
            .GroupBy(request => request.EmployeeId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<LeaveRequest>)[.. group]);

        return new Result<ListLeaveRequestsResult>.Success(new ListLeaveRequestsResult
        {
            Items = items
                .Select(request =>
                {
                    var subject = request.Employee;
                    var (canDecide, canCancel, canEdit) =
                        LeaveRequestResult.PermissionsFor(query.Caller, request, subject);

                    // No subject means no way to judge authority, so nothing sensitive is shown.
                    var canReadDetails = subject is not null
                        && LeaveRules.CanReadDetails(query.Caller, subject);
                    var canReadBalance = subject is not null
                        && LeaveRules.CanReadBalance(query.Caller, subject);

                    var approved = approvedByEmployee.GetValueOrDefault(request.EmployeeId, []);

                    return LeaveRequestResult.From(
                        request,
                        policy,
                        canReadBalance ? LeaveQuota.UsedDaysAllTypes(approved, year, policy) : null,
                        canDecide: canDecide,
                        canCancel: canCancel,
                        canEdit: canEdit,
                        canReadDetails: canReadDetails,
                        // Gated on details too, not just the balance: the block names the leave
                        // type, which is redacted from anyone without standing to read it.
                        quota: canReadBalance && canReadDetails && subject is not null
                            ? LeaveQuotaResult.For(subject, request.Type, year, today, approved, policy)
                            : null);
                })
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        });
    }
}

internal sealed class LeaveRequestListSpec : Specification<LeaveRequest>
{
    public LeaveRequestListSpec(
        int page,
        int pageSize,
        IReadOnlyList<Expression<Func<LeaveRequest, bool>>> filters,
        Caller caller)
    {
        ApplyFilters(Query, filters, caller);
        Query.Include(request => request.Employee);
        Query.OrderByDescending(request => request.RequestedAtUtc);
        Query.AsNoTracking();
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }

    internal static void ApplyFilters(
        ISpecificationBuilder<LeaveRequest> query,
        IReadOnlyList<Expression<Func<LeaveRequest, bool>>> filters,
        Caller caller)
    {
        // Every colleague sees every row: the list doubles as the company's leave calendar, so
        // "is the Owner out on Thursday?" is answerable without asking anyone. Rows are not
        // filtered by authority — the sensitive fields on them are, per row, by
        // LeaveRules.CanReadDetails / CanReadBalance in the handler's projection.
        //
        // An account with no employee record is not a colleague (see Caller), so it is not in
        // the calendar's audience and sees nothing. This comes first, so no filter row can widen it.
        if (caller.EmployeeId is null)
        {
            query.Where(_ => false);
        }

        FilterApplier.ApplyTo(query, filters);
    }
}

internal sealed class LeaveRequestListCountSpec : Specification<LeaveRequest>
{
    public LeaveRequestListCountSpec(
        IReadOnlyList<Expression<Func<LeaveRequest, bool>>> filters, Caller caller)
    {
        LeaveRequestListSpec.ApplyFilters(Query, filters, caller);
        Query.AsNoTracking();
    }
}
