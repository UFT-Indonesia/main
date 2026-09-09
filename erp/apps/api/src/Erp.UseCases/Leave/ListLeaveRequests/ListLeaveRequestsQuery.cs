using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Leave.ListLeaveRequests;

public sealed record ListLeaveRequestsQuery(
    int Page,
    int PageSize,
    IReadOnlyList<FilterRow> Filters,
    Caller Caller);

public sealed class ListLeaveRequestsResult
{
    public IReadOnlyList<Common.LeaveRequestResult> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
