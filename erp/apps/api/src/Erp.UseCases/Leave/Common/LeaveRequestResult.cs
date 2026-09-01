using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.UseCases.Common;

namespace Erp.UseCases.Leave.Common;

public sealed class LeaveRequestResult
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
    public Guid RequestedByUserId { get; init; }
    public DateTimeOffset RequestedAtUtc { get; init; }
    public string? DecidedByName { get; init; }
    public DateTimeOffset? DecidedAtUtc { get; init; }
    public string? DecisionNote { get; init; }

    /// <summary>
    /// Set only once Cancelled. Unlike Reason/DecisionNote, not gated behind canReadDetails —
    /// it's no more sensitive than the Cancelled status itself, which is already visible to
    /// everyone the request is visible to.
    /// </summary>
    public string? CancellationReason { get; init; }

    /// <summary>
    /// Approved Mon–Fri days for this employee in the current calendar year, or null when the
    /// caller may not read this employee's balance. Null rather than 0 — 0 would read as
    /// "has taken no leave", which is a different claim from "you may not see this".
    /// </summary>
    public int? ApprovedWorkdaysThisYear { get; init; }

    /// <summary>
    /// What is actually enforced for *this request's own type*, in the year it falls in. Null
    /// when the caller may not read the balance, or may not read the type — the block names the
    /// type, so it cannot be shown to someone the type itself is redacted from.
    /// </summary>
    public LeaveQuotaResult? Quota { get; init; }

    /// <summary>
    /// What the calling user may do with this request. Computed server-side because the
    /// rules depend on the subject's role and reporting line, which the client never sees.
    /// </summary>
    public bool CanDecide { get; init; }

    public bool CanCancel { get; init; }

    /// <summary>
    /// The doctor's note on a Sick request, or null when there is none — or when the caller may
    /// not read the details. Gated with Reason, not separately: the file is the same health
    /// data the reason is, and a second rule is a second thing to get wrong.
    /// </summary>
    public LeaveAttachmentResult? Attachment { get; init; }

    public static LeaveRequestResult From(
        LeaveRequest request,
        int? approvedWorkdaysThisYear = 0,
        string? employeeFullName = null,
        bool canDecide = false,
        bool canCancel = false,
        bool canReadDetails = false,
        LeaveQuotaResult? quota = null) => new()
    {
        Id = request.Id.Value,
        EmployeeId = request.EmployeeId.Value,
        EmployeeFullName = employeeFullName ?? request.Employee?.FullName ?? "—",
        Type = canReadDetails ? request.Type.ToString() : null,
        StartDate = request.StartDate.ToDateOnly(),
        EndDate = request.EndDate.ToDateOnly(),
        WorkdayCount = request.WorkdayCount,
        Reason = canReadDetails ? request.Reason : null,
        Attachment = canReadDetails && request.Attachment is { } file
            ? new LeaveAttachmentResult
            {
                FileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = file.SizeBytes,
            }
            : null,
        Status = request.Status.ToString(),
        RequestedByUserId = request.RequestedByUserId,
        RequestedAtUtc = request.RequestedAtUtc.ToDateTimeOffset(),
        DecidedByName = request.DecidedByName,
        DecidedAtUtc = request.DecidedAtUtc?.ToDateTimeOffset(),
        DecisionNote = canReadDetails ? request.DecisionNote : null,
        CancellationReason = request.CancellationReason?.ToString(),
        ApprovedWorkdaysThisYear = approvedWorkdaysThisYear,
        Quota = quota,
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

/// <summary>
/// What a client is told about an attachment. Deliberately not the storage key — the file is
/// fetched by leave request id, so the key never has to leave the server.
/// </summary>
public sealed class LeaveAttachmentResult
{
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public long SizeBytes { get; init; }
}
