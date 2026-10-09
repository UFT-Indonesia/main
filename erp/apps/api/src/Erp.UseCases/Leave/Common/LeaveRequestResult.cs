using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.UseCases.Common;
using NodaTime;

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
    /// Annual's own toggle. <see cref="HalfDayPeriod"/> says which half when true. False/null
    /// when the caller may not read this request's details — same gate as <see cref="Type"/>,
    /// because a non-null hour or half-day flag would otherwise prove the hidden type (only
    /// Permission sets hours, only Annual sets HalfDay).
    /// </summary>
    public bool HalfDay { get; init; }
    public string? HalfDayPeriod { get; init; }

    /// <summary>Izin's own toggle. Both set together, null when hidden or on any other request.</summary>
    public int? StartHour { get; init; }
    public int? EndHour { get; init; }

    /// <summary>
    /// Quota this request actually spends: WorkdayCount for a plain request, half that for a
    /// half day, an hourly fraction for Izin. Null when the caller may not read this request's
    /// details — a fractional value would itself hint at the hidden half-day/hourly shape.
    /// </summary>
    public decimal? ChargedDays { get; init; }

    /// <summary>
    /// How many of this request's days go past the quota and cost salary — as they stand for an
    /// approved one, as they would fall if approved now for a pending one (GSS03 decision 10). Days
    /// only, never rupiah. Null when the caller may not read the details, or the request is neither.
    /// </summary>
    public decimal? OverQuotaDays { get; init; }

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
    public decimal? ApprovedWorkdaysThisYear { get; init; }

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
    /// Whether the caller may move this request's dates. Same standing as deciding it
    /// (LeaveRules.CanDecideFor), and only while it is still Pending or Approved.
    /// </summary>
    public bool CanEdit { get; init; }

    /// <summary>
    /// Set together by an edit, null on a request nobody has moved. Not gated behind
    /// canReadDetails — "who moved my leave, and from when" is the employee's own business, and
    /// the dates themselves are already visible to every colleague.
    /// </summary>
    public string? EditedByName { get; init; }
    public DateTimeOffset? EditedAtUtc { get; init; }
    public DateOnly? PreviousStartDate { get; init; }
    public DateOnly? PreviousEndDate { get; init; }

    /// <summary>
    /// The doctor's note on a Sick request, or null when there is none — or when the caller may
    /// not read the details. Gated with Reason, not separately: the file is the same health
    /// data the reason is, and a second rule is a second thing to get wrong.
    /// </summary>
    public LeaveAttachmentResult? Attachment { get; init; }

    /// <summary>
    /// The earliest closed payroll month holding a workday of this request, or null. Drives the
    /// disabled-with-reason state of Edit and Cancel (GSS03 follow-up Q3).
    /// </summary>
    public DateOnly? PayrollClosedMonth { get; init; }

    /// <summary>
    /// Pending only, and only for someone who may decide it: the closed month in which approving now
    /// would cut a day — Approve is refused (follow-up Q4).
    /// </summary>
    public DateOnly? ApproveBlockedMonth { get; init; }

    /// <summary>Edit / Cancel would be allowed but for a closed payroll month — shown disabled, with the reason.</summary>
    public bool EditBlockedByPayroll { get; init; }

    public bool CancelBlockedByPayroll { get; init; }

    /// <summary>The caller is the Owner and Edit/Cancel here is a correction after close (follow-up Q8/Q16).</summary>
    public bool IsCorrection { get; init; }

    /// <summary>The latest correction after close, gated like Reason (follow-up Q13). Never any rupiah.</summary>
    public string? CorrectionReason { get; init; }

    public string? CorrectedByName { get; init; }

    public DateTimeOffset? CorrectedAtUtc { get; init; }

    public DateOnly? CorrectedMonth { get; init; }

    public static LeaveRequestResult From(
        LeaveRequest request,
        AttendanceDayPolicy policy,
        decimal? approvedWorkdaysThisYear = 0,
        string? employeeFullName = null,
        bool canDecide = false,
        bool canCancel = false,
        bool canEdit = false,
        bool canReadDetails = false,
        LeaveQuotaResult? quota = null,
        decimal? overQuotaDays = null,
        LeavePayrollState? payroll = null) => new()
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
        HalfDay = canReadDetails && request.HalfDay,
        HalfDayPeriod = canReadDetails ? request.Period?.ToString() : null,
        StartHour = canReadDetails ? request.StartHour : null,
        EndHour = canReadDetails ? request.EndHour : null,
        ChargedDays = canReadDetails ? request.TotalCharge(policy) : null,
        OverQuotaDays = canReadDetails ? overQuotaDays : null,
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
        CanEdit = canEdit,
        EditedByName = request.EditedByName,
        EditedAtUtc = request.EditedAtUtc?.ToDateTimeOffset(),
        PreviousStartDate = request.PreviousStartDate?.ToDateOnly(),
        PreviousEndDate = request.PreviousEndDate?.ToDateOnly(),
        PayrollClosedMonth = payroll?.ClosedMonth?.ToDateOnly(),
        ApproveBlockedMonth = canDecide ? payroll?.ApproveBlockedMonth?.ToDateOnly() : null,
        EditBlockedByPayroll = payroll?.EditBlocked ?? false,
        CancelBlockedByPayroll = payroll?.CancelBlocked ?? false,
        IsCorrection = payroll?.IsCorrection ?? false,
        CorrectionReason = canReadDetails ? request.CorrectionReason : null,
        CorrectedByName = canReadDetails ? request.CorrectedByName : null,
        CorrectedAtUtc = canReadDetails ? request.CorrectedAtUtc?.ToDateTimeOffset() : null,
        CorrectedMonth = canReadDetails ? request.CorrectedMonth?.ToDateOnly() : null,
    };

    /// <summary>
    /// The payroll side of one request for one caller. A closed month locks Edit (any status) and Cancel
    /// (Approved) for everyone but the Owner, whose Edit/Cancel becomes a correction after close.
    /// </summary>
    public static LeavePayrollState PayrollStateFor(
        Caller caller,
        LeaveRequest request,
        Employee? subject,
        LocalDate? closedMonth,
        LocalDate? approveBlockedMonth = null)
    {
        if (subject is null || closedMonth is null)
        {
            return new LeavePayrollState(closedMonth, approveBlockedMonth, false, false, false);
        }

        var open = request.Status is LeaveRequestStatus.Pending or LeaveRequestStatus.Approved;
        var isOwner = caller.Role == EmployeeRole.Owner;
        var wouldEdit = open && LeaveRules.CanDecideFor(caller, subject);
        var wouldCancel = open && LeaveRules.CanCancel(caller, subject);
        var cancelLocks = request.Status == LeaveRequestStatus.Approved;

        return new LeavePayrollState(
            closedMonth,
            approveBlockedMonth,
            EditBlocked: wouldEdit && !isOwner,
            CancelBlocked: wouldCancel && cancelLocks && !isOwner,
            IsCorrection: isOwner && (wouldEdit || (wouldCancel && cancelLocks)));
    }

    /// <summary>Permission flags for one request, given who is asking and who it is about.</summary>
    public static (bool CanDecide, bool CanCancel, bool CanEdit) PermissionsFor(
        Caller caller,
        LeaveRequest request,
        Employee? subject,
        LocalDate? payrollClosedMonth = null)
    {
        var (canDecide, canCancel, canEdit) = BasePermissionsFor(caller, request, subject);
        var payroll = PayrollStateFor(caller, request, subject, payrollClosedMonth);
        return (canDecide, canCancel && !payroll.CancelBlocked, canEdit && !payroll.EditBlocked);
    }

    private static (bool CanDecide, bool CanCancel, bool CanEdit) BasePermissionsFor(
        Caller caller,
        LeaveRequest request,
        Employee? subject)
    {
        if (subject is null)
        {
            return (false, false, false);
        }

        var pending = request.Status == LeaveRequestStatus.Pending;
        var open = pending || request.Status == LeaveRequestStatus.Approved;

        return (
            pending && LeaveRules.CanDecideFor(caller, subject)
                && !LeaveRules.IsRequester(caller, request.RequestedByUserId),
            open && LeaveRules.CanCancel(caller, subject),
            // Deliberately without the IsRequester bar that gates deciding: fixing a date you
            // typed yourself is exactly the mistake this exists for.
            open && LeaveRules.CanDecideFor(caller, subject));
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

/// <summary>See <see cref="LeaveRequestResult.PayrollStateFor"/>.</summary>
public sealed record LeavePayrollState(
    LocalDate? ClosedMonth, LocalDate? ApproveBlockedMonth, bool EditBlocked, bool CancelBlocked, bool IsCorrection);
