using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.UseCases.Common;
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
        IClock clock,
        CancellationToken ct)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(query.PageSize, MaxPageSize);

        LeaveRequestStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<LeaveRequestStatus>(query.Status, ignoreCase: true, out var parsed))
            {
                return new Result<ListLeaveRequestsResult>.Error(
                    "leave.status_invalid", "Status must be Pending, Approved, Denied, or Cancelled.");
            }

            statusFilter = parsed;
        }

        var employeeFilter = query.EmployeeId.HasValue ? new EmployeeId(query.EmployeeId.Value) : (EmployeeId?)null;

        var totalCount = await leaveRequests.CountAsync(
            new LeaveRequestListCountSpec(statusFilter, employeeFilter, query.Caller), ct);
        var items = await leaveRequests.ListAsync(
            new LeaveRequestListSpec(page, pageSize, statusFilter, employeeFilter, query.Caller), ct);

        // "Approved workdays this year" counter per employee on the page, one query.
        var year = clock.GetCurrentInstant().InUtc().Year;
        var employeeIds = items.Select(request => request.EmployeeId).Distinct().ToList();
        var approvedThisYear = employeeIds.Count == 0
            ? []
            : await leaveRequests.ListAsync(new ApprovedLeaveForYearSpec(employeeIds, year), ct);
        var approvedDaysByEmployee = approvedThisYear
            .GroupBy(request => request.EmployeeId)
            .ToDictionary(group => group.Key, group => group.Sum(request => request.WorkdayCount));

        return new Result<ListLeaveRequestsResult>.Success(new ListLeaveRequestsResult
        {
            Items = items
                .Select(request =>
                {
                    var (canDecide, canCancel) =
                        LeaveRequestResult.PermissionsFor(query.Caller, request, request.Employee);
                    return LeaveRequestResult.From(
                        request,
                        approvedDaysByEmployee.GetValueOrDefault(request.EmployeeId),
                        canDecide: canDecide,
                        canCancel: canCancel);
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
        LeaveRequestStatus? status,
        EmployeeId? employeeId,
        Caller caller)
    {
        ApplyFilters(Query, status, employeeId, caller);
        Query.Include(request => request.Employee);
        Query.OrderByDescending(request => request.RequestedAtUtc);
        Query.AsNoTracking();
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }

    internal static void ApplyFilters(
        ISpecificationBuilder<LeaveRequest> query,
        LeaveRequestStatus? status,
        EmployeeId? employeeId,
        Caller caller)
    {
        // Visibility mirrors authority: you see the leave you could act on, plus your own.
        // Applied in the query so paging and totals stay honest.
        ApplyCallerScope(query, caller);

        if (status.HasValue)
        {
            query.Where(request => request.Status == status.Value);
        }

        if (employeeId.HasValue)
        {
            query.Where(request => request.EmployeeId == employeeId.Value);
        }
    }

    private static void ApplyCallerScope(ISpecificationBuilder<LeaveRequest> query, Caller caller)
    {
        if (caller.Role == EmployeeRole.Owner)
        {
            return;
        }

        // An account with no employee cannot own or supervise leave, so it sees none.
        if (caller.EmployeeId is not { } callerEmployeeId)
        {
            query.Where(_ => false);
            return;
        }

        if (caller.Role == EmployeeRole.Manager)
        {
            query.Where(request => request.EmployeeId == callerEmployeeId
                                   || request.Employee!.ParentId == callerEmployeeId);
            return;
        }

        query.Where(request => request.EmployeeId == callerEmployeeId);
    }
}

internal sealed class LeaveRequestListCountSpec : Specification<LeaveRequest>
{
    public LeaveRequestListCountSpec(LeaveRequestStatus? status, EmployeeId? employeeId, Caller caller)
    {
        LeaveRequestListSpec.ApplyFilters(Query, status, employeeId, caller);
        Query.AsNoTracking();
    }
}
