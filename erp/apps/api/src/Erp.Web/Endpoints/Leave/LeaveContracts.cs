using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.GetLeaveBalance;

namespace Erp.Web.Endpoints.Leave;

/// <summary>
/// Multipart, not JSON, because Sick leave carries a doctor's note. <see cref="Attachment"/> is
/// required for Sick and must be absent otherwise — enforced by the domain, not here.
/// </summary>
public sealed class CreateLeaveRequestRequest
{
    public Guid EmployeeId { get; init; }
    public string Type { get; init; } = default!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Reason { get; init; } = default!;
    public IFormFile? Attachment { get; init; }

    /// <summary>Annual's own toggle.</summary>
    public bool HalfDay { get; init; }
    /// <summary>"Morning" or "Afternoon". Required when <see cref="HalfDay"/> is true.</summary>
    public string? HalfDayPeriod { get; init; }

    /// <summary>Izin's own toggle. Both required together, whole hours, 12:00 excluded.</summary>
    public int? StartHour { get; init; }
    public int? EndHour { get; init; }
}

public sealed class GetLeaveAttachmentRequest
{
    public Guid Id { get; init; }
}

public sealed class LeaveAttachmentResponse
{
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public long SizeBytes { get; init; }
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

/// <summary>The window is required — see GetBlockedLeaveDatesHandler for why it is not optional.</summary>
public sealed class GetBlockedLeaveDatesRequest
{
    public Guid EmployeeId { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    /// <summary>The in-progress request's own shape — see GetBlockedLeaveDatesQuery.</summary>
    public bool HalfDay { get; init; }
    public string? HalfDayPeriod { get; init; }
    public int? StartHour { get; init; }
    public int? EndHour { get; init; }
}

public sealed class BlockedLeaveDatesResponse
{
    public IReadOnlyList<DateOnly> BlockedDates { get; init; } = [];
    public IReadOnlyList<DateOnly> PartialDates { get; init; } = [];
}

/// <summary>
/// Null entitled/remaining means uncapped — an owner, or a type with no override. Remaining may
/// be negative for someone already over a cap that was set after the fact; it is reported raw
/// rather than clamped, so the overage is visible.
/// </summary>
public sealed class LeaveQuotaResponse
{
    public string Type { get; init; } = default!;
    public decimal? EntitledDays { get; init; }
    public decimal UsedDays { get; init; }
    public decimal? RemainingDays { get; init; }

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

/// <summary>
/// Full replacement of the request's date range and half-day/hourly shape. Type, reason and
/// attachment are absent on purpose — see EditLeaveRequestCommand.
/// </summary>
public sealed class EditLeaveRequestRequest
{
    public Guid Id { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool HalfDay { get; init; }
    /// <summary>"Morning" or "Afternoon". Required when <see cref="HalfDay"/> is true.</summary>
    public string? HalfDayPeriod { get; init; }
    public int? StartHour { get; init; }
    public int? EndHour { get; init; }
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

    /// <summary>The doctor's note on a Sick request; null when there is none, or is not readable.</summary>
    public LeaveAttachmentResponse? Attachment { get; init; }
    public string Status { get; init; } = default!;
    public DateTimeOffset RequestedAtUtc { get; init; }
    public string? DecidedByName { get; init; }
    public DateTimeOffset? DecidedAtUtc { get; init; }
    public string? DecisionNote { get; init; }

    /// <summary>
    /// Annual's own toggle. <see cref="HalfDayPeriod"/> says which half when true. False/null
    /// when the caller may not read this request's details, same as <see cref="Type"/>.
    /// </summary>
    public bool HalfDay { get; init; }
    public string? HalfDayPeriod { get; init; }

    /// <summary>Izin's own toggle. Both set together, null when hidden or on any other request.</summary>
    public int? StartHour { get; init; }
    public int? EndHour { get; init; }

    /// <summary>Quota this request actually spends — see LeaveRequestResult.ChargedDays.</summary>
    public decimal? ChargedDays { get; init; }

    /// <summary>Set only once Status is Cancelled.</summary>
    public string? CancellationReason { get; init; }
    /// <summary>Null when the caller may not read this employee's leave balance.</summary>
    public decimal? ApprovedWorkdaysThisYear { get; init; }

    /// <summary>
    /// What is enforced for this request's own type. Null when the caller may not read the
    /// balance, or may not read the type the block would name.
    /// </summary>
    public LeaveQuotaResponse? Quota { get; init; }

    /// <summary>What the calling user may do with this request — drives which controls the UI renders.</summary>
    public bool CanDecide { get; init; }
    public bool CanCancel { get; init; }
    public bool CanEdit { get; init; }

    /// <summary>Set together by an edit; null on a request nobody has moved.</summary>
    public string? EditedByName { get; init; }
    public DateTimeOffset? EditedAtUtc { get; init; }
    public DateOnly? PreviousStartDate { get; init; }
    public DateOnly? PreviousEndDate { get; init; }

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
        Attachment = result.Attachment is { } file
            ? new LeaveAttachmentResponse
            {
                FileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = file.SizeBytes,
            }
            : null,
        HalfDay = result.HalfDay,
        HalfDayPeriod = result.HalfDayPeriod,
        StartHour = result.StartHour,
        EndHour = result.EndHour,
        ChargedDays = result.ChargedDays,
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
        CanEdit = result.CanEdit,
        EditedByName = result.EditedByName,
        EditedAtUtc = result.EditedAtUtc,
        PreviousStartDate = result.PreviousStartDate,
        PreviousEndDate = result.PreviousEndDate,
    };
}

public sealed class ListLeaveRequestsResponse
{
    public IReadOnlyList<LeaveRequestResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
