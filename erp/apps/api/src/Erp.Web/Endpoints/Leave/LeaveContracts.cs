using Erp.UseCases.Leave.Common;

namespace Erp.Web.Endpoints.Leave;

public sealed class CreateLeaveRequestRequest
{
    public Guid EmployeeId { get; init; }
    public string Type { get; init; } = default!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string? Reason { get; init; }
}

public sealed class ListLeaveRequestsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Status { get; init; }
    public Guid? EmployeeId { get; init; }
}

public sealed class DecideLeaveRequestRequest
{
    public Guid Id { get; init; }
    public string? Note { get; init; }
}

public sealed class LeaveRequestResponse
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public string Type { get; init; } = default!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public int WorkdayCount { get; init; }
    public string? Reason { get; init; }
    public string Status { get; init; } = default!;
    public DateTimeOffset RequestedAtUtc { get; init; }
    public string? DecidedByName { get; init; }
    public DateTimeOffset? DecidedAtUtc { get; init; }
    public string? DecisionNote { get; init; }
    public int ApprovedWorkdaysThisYear { get; init; }

    public static LeaveRequestResponse From(LeaveRequestResult result) => new()
    {
        Id = result.Id,
        EmployeeId = result.EmployeeId,
        EmployeeFullName = result.EmployeeFullName,
        Type = result.Type,
        StartDate = result.StartDate,
        EndDate = result.EndDate,
        WorkdayCount = result.WorkdayCount,
        Reason = result.Reason,
        Status = result.Status,
        RequestedAtUtc = result.RequestedAtUtc,
        DecidedByName = result.DecidedByName,
        DecidedAtUtc = result.DecidedAtUtc,
        DecisionNote = result.DecisionNote,
        ApprovedWorkdaysThisYear = result.ApprovedWorkdaysThisYear,
    };
}

public sealed class ListLeaveRequestsResponse
{
    public IReadOnlyList<LeaveRequestResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
