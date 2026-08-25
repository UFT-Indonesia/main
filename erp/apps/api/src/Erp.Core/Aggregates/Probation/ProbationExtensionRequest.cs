using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Probation;

/// <summary>
/// A Manager's request for more probation time for one of their own Staff, decided by an Owner.
/// Lifecycle: Pending → Approved | Denied | Cancelled, mirroring <c>LeaveRequest</c>. Denied and
/// Cancelled are terminal; a wrong date is fixed by cancelling and filing again, never by editing.
/// <para>
/// The request carries the exact <see cref="ProposedEndsOn"/> that approval will write, rather
/// than a duration re-derived at decision time — so a base date shifting between filing and
/// approval cannot change what the Owner agreed to.
/// </para>
/// </summary>
public sealed class ProbationExtensionRequest : Entity<ProbationExtensionRequestId>
{
    public const int ReasonMaxLength = 500;
    public const int DecisionNoteMaxLength = 500;

    // EF Core constructor.
    private ProbationExtensionRequest() { }

    private ProbationExtensionRequest(
        ProbationExtensionRequestId id,
        EmployeeId employeeId,
        LocalDate currentEndsOn,
        LocalDate proposedEndsOn,
        string reason,
        Guid requestedByUserId,
        Instant requestedAtUtc)
        : base(id)
    {
        EmployeeId = employeeId;
        CurrentEndsOn = currentEndsOn;
        ProposedEndsOn = proposedEndsOn;
        Reason = reason;
        Status = ProbationExtensionStatus.Pending;
        RequestedByUserId = requestedByUserId;
        RequestedAtUtc = requestedAtUtc;
    }

    public EmployeeId EmployeeId { get; private set; }

    // EF Core navigation — read-only, not part of domain behavior.
    public Employee? Employee { get; private set; }

    /// <summary>Snapshot of the probation end at filing time, so the decider sees the delta asked for.</summary>
    public LocalDate CurrentEndsOn { get; private set; }

    /// <summary>The date approval writes to the employee's probation override.</summary>
    public LocalDate ProposedEndsOn { get; private set; }

    /// <summary>Required — extending someone's probation is not a decision to make unexplained.</summary>
    public string Reason { get; private set; } = default!;

    public ProbationExtensionStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public Instant RequestedAtUtc { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    /// <summary>Display-name snapshot of the decider, so the trail survives renames.</summary>
    public string? DecidedByName { get; private set; }

    public Instant? DecidedAtUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    public static ProbationExtensionRequest Create(
        EmployeeId employeeId,
        LocalDate currentEndsOn,
        LocalDate proposedEndsOn,
        string reason,
        Guid requestedByUserId,
        Instant requestedAtUtc)
    {
        if (employeeId == EmployeeId.Empty)
        {
            throw new DomainException("probation.employee_id", "Employee id is required.");
        }

        if (requestedByUserId == Guid.Empty)
        {
            throw new DomainException(
                "probation.requested_by", "Extension requests require an authenticated requester.");
        }

        if (proposedEndsOn <= currentEndsOn)
        {
            throw new DomainException(
                "probation.not_an_extension",
                "The proposed date must be later than the current probation end date.");
        }

        var trimmedReason = reason?.Trim();
        if (string.IsNullOrEmpty(trimmedReason))
        {
            throw new DomainException("probation.reason", "A reason is required to extend probation.");
        }

        if (trimmedReason.Length > ReasonMaxLength)
        {
            throw new DomainException(
                "probation.reason_length", $"Reason cannot exceed {ReasonMaxLength} characters.");
        }

        return new ProbationExtensionRequest(
            ProbationExtensionRequestId.New(),
            employeeId,
            currentEndsOn,
            proposedEndsOn,
            trimmedReason,
            requestedByUserId,
            requestedAtUtc);
    }

    public void Approve(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note)
    {
        EnsurePending("approve");
        SetDecision(decidedByUserId, decidedByName, nowUtc, note);
        Status = ProbationExtensionStatus.Approved;
    }

    public void Deny(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note)
    {
        EnsurePending("deny");
        SetDecision(decidedByUserId, decidedByName, nowUtc, note);
        Status = ProbationExtensionStatus.Denied;
    }

    /// <summary>Withdrawal by the filing Manager. Only meaningful while still Pending.</summary>
    public void Cancel(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note)
    {
        EnsurePending("cancel");
        SetDecision(decidedByUserId, decidedByName, nowUtc, note);
        Status = ProbationExtensionStatus.Cancelled;
    }

    private void EnsurePending(string action)
    {
        if (Status != ProbationExtensionStatus.Pending)
        {
            throw new DomainException(
                "probation.not_pending",
                $"Only pending extension requests can be {action}d (status: {Status}).");
        }
    }

    private void SetDecision(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note)
    {
        if (decidedByUserId == Guid.Empty)
        {
            throw new DomainException("probation.decided_by", "Decisions require an authenticated user.");
        }

        if (string.IsNullOrWhiteSpace(decidedByName))
        {
            throw new DomainException(
                "probation.decided_by_name", "Decisions require the decider's display name.");
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > DecisionNoteMaxLength })
        {
            throw new DomainException(
                "probation.note_length", $"Decision note cannot exceed {DecisionNoteMaxLength} characters.");
        }

        DecidedByUserId = decidedByUserId;
        DecidedByName = decidedByName.Trim();
        DecidedAtUtc = nowUtc;
        DecisionNote = trimmedNote;
    }
}
