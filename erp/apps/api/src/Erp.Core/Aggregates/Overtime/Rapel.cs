using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Overtime;

/// <summary>
/// A late claim for a closed Gaji Premi period, decided by the Owner, who sets the amount.
/// Paid as one line on the next payout, labelled with its work date. Carries no counted hours.
/// </summary>
public sealed class Rapel : AggregateRoot<RapelId>
{
    // EF Core constructor.
    private Rapel() { }

    private Rapel(
        RapelId id, EmployeeId employeeId, LocalDate workDate, string note, LeaveAttachment attachment,
        Guid requestedByUserId, Instant requestedAtUtc)
        : base(id)
    {
        EmployeeId = employeeId;
        WorkDate = workDate;
        Note = note;
        Attachment = attachment;
        Status = OvertimeRequestStatus.Pending;
        RequestedByUserId = requestedByUserId;
        RequestedAtUtc = requestedAtUtc;
    }

    public EmployeeId EmployeeId { get; private set; }

    // EF Core navigation — read-only, not part of domain behavior.
    public Employee? Employee { get; private set; }

    /// <summary>Inside the closed period being claimed.</summary>
    public LocalDate WorkDate { get; private set; }

    public string Note { get; private set; } = default!;

    public LeaveAttachment Attachment { get; private set; } = default!;

    public OvertimeRequestStatus Status { get; private set; }

    /// <summary>Set by the Owner on approval.</summary>
    public decimal? Amount { get; private set; }

    /// <summary>Start of the period whose payout carries this line; set on approval.</summary>
    public LocalDate? PayoutPeriodStart { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public Instant RequestedAtUtc { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public string? DecidedByName { get; private set; }

    public Instant? DecidedAtUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    public static Rapel Create(
        EmployeeId employeeId, LocalDate workDate, string note, LeaveAttachment? attachment,
        Guid requestedByUserId, Instant nowUtc) =>
        new(RapelId.New(), employeeId, workDate, OvertimeCorrectionRequest.ValidReason(note),
            attachment ?? throw new DomainException("rapel.proof_required", "A WA proof is required."),
            requestedByUserId, nowUtc);

    public void Approve(decimal amount, LocalDate payoutPeriodStart, Guid by, string byName, Instant now)
    {
        if (amount <= 0)
        {
            throw new DomainException("rapel.amount", "Rapel amount must be greater than zero.");
        }

        Decide(OvertimeRequestStatus.Approved, by, byName, now, null);
        Amount = amount;
        PayoutPeriodStart = payoutPeriodStart;
    }

    public void Reject(Guid by, string byName, Instant now, string? note) =>
        Decide(OvertimeRequestStatus.Rejected, by, byName, now, note);

    private void Decide(OvertimeRequestStatus status, Guid by, string byName, Instant now, string? note)
    {
        if (Status != OvertimeRequestStatus.Pending)
        {
            throw new DomainException("rapel.not_pending", $"Only pending rapel can be decided (status: {Status}).");
        }

        (DecidedByUserId, DecidedByName, DecidedAtUtc, DecisionNote) = OvertimeDecision.Stamp(by, byName, now, note);
        Status = status;
    }
}
