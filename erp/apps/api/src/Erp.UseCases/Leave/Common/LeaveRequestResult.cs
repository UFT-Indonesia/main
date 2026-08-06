using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.UseCases.Common;

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

    /// <summary>
    /// What the calling user may do with this request. Computed server-side because the
    /// rules depend on the subject's role and reporting line, which the client never sees.
    /// </summary>
    public bool CanDecide { get; init; }

    public bool CanCancel { get; init; }

    public static LeaveRequestResult From(
        LeaveRequest request,
        int approvedWorkdaysThisYear = 0,
        string? employeeFullName = null,
        bool canDecide = false,
        bool canCancel = false) => new()
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
        CanDecide = canDecide,
        CanCancel = canCancel,
    };

    /// <summary>Permission flags for one request, given who is asking and who it is about.</summary>
    public static (bool CanDecide, bool CanCancel) PermissionsFor(
        Caller caller,
        LeaveRequest request,
        Employee? subject)
    {
        if (subject is null)
        {
            return (false, false);
        }

        var pending = request.Status == LeaveRequestStatus.Pending;
        var open = pending || request.Status == LeaveRequestStatus.Approved;

        return (
            pending && LeaveRules.CanDecideFor(caller, subject)
                && !LeaveRules.IsRequester(caller, request.RequestedByUserId),
            open && LeaveRules.CanCancel(caller, subject));
    }
}
