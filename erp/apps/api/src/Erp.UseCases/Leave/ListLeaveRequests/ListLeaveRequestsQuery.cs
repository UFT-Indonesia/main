namespace Erp.UseCases.Leave.ListLeaveRequests;

public sealed record ListLeaveRequestsQuery(
    int Page,
    int PageSize,
    string? Status,
    Guid? EmployeeId);

public sealed class ListLeaveRequestsResult
{
    public IReadOnlyList<Common.LeaveRequestResult> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
