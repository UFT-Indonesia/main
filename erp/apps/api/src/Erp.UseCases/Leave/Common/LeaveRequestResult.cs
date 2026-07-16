using Erp.Core.Aggregates.Leave;

namespace Erp.UseCases.Leave.Common;

public sealed class LeaveRequestResult
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
    public Guid RequestedByUserId { get; init; }
    public DateTimeOffset RequestedAtUtc { get; init; }
    public string? DecidedByName { get; init; }
    public DateTimeOffset? DecidedAtUtc { get; init; }
    public string? DecisionNote { get; init; }

    /// <summary>Approved Mon–Fri days for this employee in the current calendar year.</summary>
    public int ApprovedWorkdaysThisYear { get; init; }

    public static LeaveRequestResult From(
        LeaveRequest request,
        int approvedWorkdaysThisYear = 0,
        string? employeeFullName = null) => new()
    {
        Id = request.Id.Value,
        EmployeeId = request.EmployeeId.Value,
        EmployeeFullName = employeeFullName ?? request.Employee?.FullName ?? "—",
        Type = request.Type.ToString(),
        StartDate = request.StartDate.ToDateOnly(),
        EndDate = request.EndDate.ToDateOnly(),
        WorkdayCount = request.WorkdayCount,
        Reason = request.Reason,
        Status = request.Status.ToString(),
        RequestedByUserId = request.RequestedByUserId,
        RequestedAtUtc = request.RequestedAtUtc.ToDateTimeOffset(),
        DecidedByName = request.DecidedByName,
        DecidedAtUtc = request.DecidedAtUtc?.ToDateTimeOffset(),
        DecisionNote = request.DecisionNote,
        ApprovedWorkdaysThisYear = approvedWorkdaysThisYear,
    };
}
