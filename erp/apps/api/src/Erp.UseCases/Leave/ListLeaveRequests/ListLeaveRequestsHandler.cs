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
                    var subject = request.Employee;
                    var (canDecide, canCancel) =
                        LeaveRequestResult.PermissionsFor(query.Caller, request, subject);

                    // No subject means no way to judge authority, so nothing sensitive is shown.
                    var canReadDetails = subject is not null
                        && LeaveRules.CanReadDetails(query.Caller, subject);
                    var canReadBalance = subject is not null
                        && LeaveRules.CanReadBalance(query.Caller, subject);

                    return LeaveRequestResult.From(
                        request,
                        canReadBalance
                            ? approvedDaysByEmployee.GetValueOrDefault(request.EmployeeId)
                            : null,
                        canDecide: canDecide,
                        canCancel: canCancel,
                        canReadDetails: canReadDetails);
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
        // Every authenticated user sees every row: the list doubles as the company's leave
        // calendar, so "is the Owner out on Thursday?" is answerable without asking anyone.
        // Rows are not filtered by authority — the sensitive fields on them are, per row, by
        // LeaveRules.CanReadDetails / CanReadBalance in the handler's projection.
        if (status.HasValue)
        {
            query.Where(request => request.Status == status.Value);
        }

        if (employeeId.HasValue)
        {
            query.Where(request => request.EmployeeId == employeeId.Value);
        }
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
