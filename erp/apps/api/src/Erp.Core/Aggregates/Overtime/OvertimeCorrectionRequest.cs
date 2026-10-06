using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Overtime;

public enum OvertimeCorrectionKind
{
    TapIn = 0,
    TapOut = 1,
}

/// <summary>
/// An employee asking for a missing OT tap-in or tap-out to be written. Approval turns into an
/// <c>AttendanceLog.Manual</c> punch — see ApproveOvertimeCorrectionHandler — so nothing here
/// touches attendance itself. Counts toward the assignment's work date, never the approval date.
/// </summary>
public sealed class OvertimeCorrectionRequest : AggregateRoot<OvertimeCorrectionRequestId>
{
    // EF Core constructor.
    private OvertimeCorrectionRequest() { }

    private OvertimeCorrectionRequest(
        OvertimeCorrectionRequestId id, OvertimeAssignmentId assignmentId, EmployeeId employeeId, LocalDate workDate,
        OvertimeCorrectionKind kind, Instant punchedAtUtc, string reason, LeaveAttachment attachment,
        Guid requestedByUserId, Instant requestedAtUtc)
        : base(id)
    {
        AssignmentId = assignmentId;
        EmployeeId = employeeId;
        WorkDate = workDate;
        Kind = kind;
        PunchedAtUtc = punchedAtUtc;
        Reason = reason;
        Attachment = attachment;
        Status = OvertimeRequestStatus.Pending;
        RequestedByUserId = requestedByUserId;
        RequestedAtUtc = requestedAtUtc;
    }

    public OvertimeAssignmentId AssignmentId { get; private set; }

    public EmployeeId EmployeeId { get; private set; }

    // EF Core navigation — read-only, not part of domain behavior.
    public Employee? Employee { get; private set; }

    /// <summary>The assignment's date, copied so period close can expire requests without a join.</summary>
    public LocalDate WorkDate { get; private set; }

    public OvertimeCorrectionKind Kind { get; private set; }

    public Instant PunchedAtUtc { get; private set; }

    public string Reason { get; private set; } = default!;

    public LeaveAttachment Attachment { get; private set; } = default!;

    public OvertimeRequestStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public Instant RequestedAtUtc { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public string? DecidedByName { get; private set; }

    public Instant? DecidedAtUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    public static OvertimeCorrectionRequest Create(
        OvertimeAssignmentId assignmentId, EmployeeId employeeId, LocalDate workDate, OvertimeCorrectionKind kind,
        Instant punchedAtUtc, string reason, LeaveAttachment? attachment, Guid requestedByUserId, Instant nowUtc)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("overtime_correction.kind", "Correction must be for a TapIn or a TapOut.");
        }

        return new OvertimeCorrectionRequest(
            OvertimeCorrectionRequestId.New(), assignmentId, employeeId, workDate, kind, punchedAtUtc,
            ValidReason(reason), attachment ?? throw new DomainException(
                "overtime_correction.proof_required", "A proof of the missing punch is required."),
            requestedByUserId, nowUtc);
    }

    /// <summary>Reason bounds borrowed from leave: an unexplained request is not reviewable.</summary>
    internal static string ValidReason(string? reason)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < LeaveRequest.ReasonMinLength)
        {
            throw new DomainException(
                "overtime.reason_required", $"A reason of at least {LeaveRequest.ReasonMinLength} characters is required.");
        }

        if (trimmed.Length > LeaveRequest.ReasonMaxLength)
        {
            throw new DomainException(
                "overtime.reason_length", $"Reason cannot exceed {LeaveRequest.ReasonMaxLength} characters.");
        }

        return trimmed;
    }

    public void Approve(Guid decidedByUserId, string decidedByName, Instant nowUtc) =>
        Decide(OvertimeRequestStatus.Approved, decidedByUserId, decidedByName, nowUtc, null);

    public void Reject(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note) =>
        Decide(OvertimeRequestStatus.Rejected, decidedByUserId, decidedByName, nowUtc, note);

    /// <summary>Period close: still-undecided requests lapse and the punch stays missing.</summary>
    public void Expire()
    {
        if (Status == OvertimeRequestStatus.Pending)
        {
            Status = OvertimeRequestStatus.Expired;
        }
    }

    private void Decide(OvertimeRequestStatus status, Guid by, string byName, Instant now, string? note)
    {
        if (Status != OvertimeRequestStatus.Pending)
        {
            throw new DomainException(
                "overtime_correction.not_pending", $"Only pending requests can be decided (status: {Status}).");
        }

        (DecidedByUserId, DecidedByName, DecidedAtUtc, DecisionNote) = OvertimeDecision.Stamp(by, byName, now, note);
        Status = status;
    }
}

/// <summary>Validation shared by every decision on a correction or rapel.</summary>
internal static class OvertimeDecision
{
    internal static (Guid, string, Instant, string?) Stamp(Guid by, string byName, Instant now, string? note)
    {
        if (by == Guid.Empty || string.IsNullOrWhiteSpace(byName))
        {
            throw new DomainException("overtime.decided_by", "Decisions require an authenticated user.");
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed is { Length: > OvertimeAssignment.DecisionNoteMaxLength })
        {
            throw new DomainException(
                "overtime.note_length", $"Decision note cannot exceed {OvertimeAssignment.DecisionNoteMaxLength} characters.");
        }

        return (by, byName.Trim(), now, trimmed);
    }
}
