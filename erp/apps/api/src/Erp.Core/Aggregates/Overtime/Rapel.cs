using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Overtime;

/// <summary>
/// A late claim on one approved <see cref="OvertimeAssignment"/> of a closed Gaji Premi period,
/// decided by the Owner. The employee says the times they worked; the system suggests the amount
/// (the tier those times reach, minus what the assignment already paid at close) and the Owner
/// approves it or overrides it. Paid as one line on the next payout, labelled with its work date.
/// </summary>
public sealed class Rapel : AggregateRoot<RapelId>
{
    // EF Core constructor.
    private Rapel() { }

    private Rapel(
        RapelId id, OvertimeAssignment assignment, LocalTime claimedStart, LocalTime claimedEnd, int claimedHours,
        decimal suggestedAmount, string note, LeaveAttachment attachment, Guid requestedByUserId, Instant requestedAtUtc)
        : base(id)
    {
        AssignmentId = assignment.Id;
        EmployeeId = assignment.EmployeeId;
        WorkDate = assignment.Date;
        ClaimedStart = claimedStart;
        ClaimedEnd = claimedEnd;
        ClaimedHours = claimedHours;
        SuggestedAmount = suggestedAmount;
        Note = note;
        Attachment = attachment;
        Status = OvertimeRequestStatus.Pending;
        RequestedByUserId = requestedByUserId;
        RequestedAtUtc = requestedAtUtc;
    }

    public EmployeeId EmployeeId { get; private set; }

    // EF Core navigation — read-only, not part of domain behavior.
    public Employee? Employee { get; private set; }

    public OvertimeAssignmentId AssignmentId { get; private set; }

    /// <summary>The assignment's date, inside the closed period being claimed.</summary>
    public LocalDate WorkDate { get; private set; }

    /// <summary>The times the employee says they worked; before 05:00 means the next morning.</summary>
    public LocalTime ClaimedStart { get; private set; }

    public LocalTime ClaimedEnd { get; private set; }

    /// <summary>Counted hours of the claimed times, by the same rules as the punches.</summary>
    public int ClaimedHours { get; private set; }

    /// <summary>Tier of <see cref="ClaimedHours"/> minus what the assignment already paid. Always above zero.</summary>
    public decimal SuggestedAmount { get; private set; }

    public string Note { get; private set; } = default!;

    public LeaveAttachment Attachment { get; private set; } = default!;

    public OvertimeRequestStatus Status { get; private set; }

    /// <summary>Set on approval: the Owner's figure, or <see cref="SuggestedAmount"/> when they keep it.</summary>
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
        OvertimeAssignment assignment, LocalTime claimedStart, LocalTime claimedEnd, string note, LeaveAttachment? attachment,
        AttendanceDayPolicy policy, IEnumerable<OvertimeTier> tiers, Guid requestedByUserId, Instant nowUtc)
    {
        if (assignment.Status != OvertimeStatus.Approved || !assignment.IsFrozen)
        {
            throw new DomainException(
                "rapel.not_claimable", "Only an approved overtime in a closed period can be claimed.");
        }

        var hours = ClaimHours(assignment, claimedStart, claimedEnd, policy);
        var suggested = OvertimeCalculator.Pay(hours, tiers) - (assignment.FrozenAmount ?? 0m);
        if (suggested <= 0)
        {
            throw new DomainException(
                "rapel.adds_nothing", "These hours pay no more than this overtime already paid.");
        }

        return new Rapel(
            RapelId.New(), assignment, claimedStart, claimedEnd, hours, suggested, OvertimeCorrectionRequest.ValidReason(note),
            attachment ?? throw new DomainException("rapel.proof_required", "A WA proof is required."),
            requestedByUserId, nowUtc);
    }

    /// <summary>
    /// The claimed times read as punches on the assignment's 05:00 → 05:00 day: clamped to the
    /// window, grace and the day-off lunch applied, rounded down — so a claim can never count more
    /// than the punches could have.
    /// </summary>
    public static int ClaimHours(OvertimeAssignment assignment, LocalTime start, LocalTime end, AttendanceDayPolicy policy)
    {
        Instant At(LocalTime t) =>
            (t < OvertimeAssignment.DayBoundary ? assignment.Date.PlusDays(1) : assignment.Date)
                .At(t).InZoneLeniently(policy.TimeZone).ToInstant();

        var (from, to) = (At(start), At(end));
        if (to <= from)
        {
            throw new DomainException("rapel.window", "The claimed end must be after the claimed start.");
        }

        return OvertimeCalculator.Count(assignment, from, to, policy).Hours;
    }

    /// <summary>Null <paramref name="amount"/> keeps the suggested figure.</summary>
    public void Approve(decimal? amount, LocalDate payoutPeriodStart, Guid by, string byName, Instant now)
    {
        amount ??= SuggestedAmount;
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
