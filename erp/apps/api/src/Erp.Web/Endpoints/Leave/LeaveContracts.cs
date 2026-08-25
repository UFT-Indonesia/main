using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.GetLeaveBalance;

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

public sealed class GetLeaveBalanceRequest
{
    public Guid EmployeeId { get; init; }
    /// <summary>Null means the current year.</summary>
    public int? Year { get; init; }
}

/// <summary>
/// Null entitled/remaining means uncapped — an owner, or a type with no override. Remaining may
/// be negative for someone already over a cap that was set after the fact; it is reported raw
/// rather than clamped, so the overage is visible.
/// </summary>
public sealed class LeaveQuotaResponse
{
    public string Type { get; init; } = default!;
    public int? EntitledDays { get; init; }
    public int UsedDays { get; init; }
    public int? RemainingDays { get; init; }

    public static LeaveQuotaResponse From(LeaveQuotaResult result) => new()
    {
        Type = result.Type,
        EntitledDays = result.EntitledDays,
        UsedDays = result.UsedDays,
        RemainingDays = result.RemainingDays,
    };
}

public sealed class LeaveBalanceResponse
{
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public int Year { get; init; }
    public bool OnProbation { get; init; }
    public DateOnly? ProbationEndsOn { get; init; }
    public IReadOnlyList<LeaveQuotaResponse> Quotas { get; init; } = [];

    public static LeaveBalanceResponse From(LeaveBalanceResult result) => new()
    {
        EmployeeId = result.EmployeeId,
        EmployeeFullName = result.EmployeeFullName,
        Year = result.Year,
        OnProbation = result.OnProbation,
        ProbationEndsOn = result.ProbationEndsOn,
        Quotas = result.Quotas.Select(LeaveQuotaResponse.From).ToList(),
    };
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

    /// <summary>Null when the caller may not read this request's details — Sick is health data.</summary>
    public string? Type { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public int WorkdayCount { get; init; }
    public string? Reason { get; init; }
    public string Status { get; init; } = default!;
    public DateTimeOffset RequestedAtUtc { get; init; }
    public string? DecidedByName { get; init; }
    public DateTimeOffset? DecidedAtUtc { get; init; }
    public string? DecisionNote { get; init; }
    /// <summary>Set only once Status is Cancelled.</summary>
    public string? CancellationReason { get; init; }
    /// <summary>Null when the caller may not read this employee's leave balance.</summary>
    public int? ApprovedWorkdaysThisYear { get; init; }

    /// <summary>
    /// What is enforced for this request's own type. Null when the caller may not read the
    /// balance, or may not read the type the block would name.
    /// </summary>
    public LeaveQuotaResponse? Quota { get; init; }

    /// <summary>What the calling user may do with this request — drives which controls the UI renders.</summary>
    public bool CanDecide { get; init; }
    public bool CanCancel { get; init; }

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
        CancellationReason = result.CancellationReason,
        ApprovedWorkdaysThisYear = result.ApprovedWorkdaysThisYear,
        Quota = result.Quota is null ? null : LeaveQuotaResponse.From(result.Quota),
        CanDecide = result.CanDecide,
        CanCancel = result.CanCancel,
    };
}

public sealed class ListLeaveRequestsResponse
{
    public IReadOnlyList<LeaveRequestResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
