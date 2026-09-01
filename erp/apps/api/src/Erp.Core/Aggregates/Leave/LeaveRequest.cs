using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave.Events;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Leave;

/// <summary>
/// A full-day leave request, filed by the employee themselves or on their behalf by
/// someone with the authority to (see LeaveRules.CanFileFor). Lifecycle:
/// Pending → Approved | Denied | Cancelled, and Approved → Cancelled.
/// Denied/Cancelled are terminal; wrong dates are fixed by cancel + resubmit, never edit.
/// </summary>
public sealed class LeaveRequest : AggregateRoot<LeaveRequestId>
{
    public const int ReasonMinLength = 2;
    public const int ReasonMaxLength = 500;
    public const int DecisionNoteMaxLength = 500;

    /// <summary>Stands in for a human decider on decisions the system makes on its own.</summary>
    public const string SystemDecider = "System";

    // EF Core constructor.
    private LeaveRequest() { }

    private LeaveRequest(
        LeaveRequestId id,
        EmployeeId employeeId,
        LeaveType type,
        LocalDate startDate,
        LocalDate endDate,
        int workdayCount,
        string reason,
        Guid requestedByUserId,
        Instant requestedAtUtc)
        : base(id)
    {
        EmployeeId = employeeId;
        Type = type;
        StartDate = startDate;
        EndDate = endDate;
        WorkdayCount = workdayCount;
        Reason = reason;
        Status = LeaveRequestStatus.Pending;
        RequestedByUserId = requestedByUserId;
        RequestedAtUtc = requestedAtUtc;
    }

    public EmployeeId EmployeeId { get; private set; }

    // EF Core navigation — read-only, not part of domain behavior.
    public Employee? Employee { get; private set; }

    public LeaveType Type { get; private set; }

    /// <summary>First day of leave, inclusive.</summary>
    public LocalDate StartDate { get; private set; }

    /// <summary>Last day of leave, inclusive.</summary>
    public LocalDate EndDate { get; private set; }

    /// <summary>Mon–Fri days inside the range, computed at creation.</summary>
    public int WorkdayCount { get; private set; }

    public string Reason { get; private set; } = default!;

    public LeaveRequestStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public Instant RequestedAtUtc { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    /// <summary>Display-name snapshot of the decider, so the trail survives renames.</summary>
    public string? DecidedByName { get; private set; }

    public Instant? DecidedAtUtc { get; private set; }

    /// <summary>Optional note recorded on deny/cancel.</summary>
    public string? DecisionNote { get; private set; }

    /// <summary>
    /// Set only by <see cref="Cancel"/>. Null on every other status, and on the system's own
    /// termination cleanup — neither reason is true of a request nobody chose to call off.
    /// </summary>
    public LeaveCancellationReason? CancellationReason { get; private set; }

    public static LeaveRequest Create(
        EmployeeId employeeId,
        LeaveType type,
        LocalDate startDate,
        LocalDate endDate,
        string reason,
        Guid requestedByUserId,
        Instant requestedAtUtc)
    {
        if (employeeId == EmployeeId.Empty)
        {
            throw new DomainException("leave.employee_id", "Employee id is required.");
        }

        if (requestedByUserId == Guid.Empty)
        {
            throw new DomainException("leave.requested_by", "Leave requests require an authenticated requester.");
        }

        if (startDate > endDate)
        {
            throw new DomainException("leave.date_range", "Start date must be on or before end date.");
        }

        var workdays = CountWorkdays(startDate, endDate);
        if (workdays == 0)
        {
            throw new DomainException("leave.no_workdays", "Leave range contains no working days (Mon–Fri).");
        }

        // Required since 2026-08: an absence with no stated reason is not reviewable. Mirrors
        // ProbationExtensionRequest.Create, which has always demanded one.
        var trimmedReason = reason?.Trim();
        if (string.IsNullOrEmpty(trimmedReason) || trimmedReason.Length < ReasonMinLength)
        {
            throw new DomainException(
                "leave.reason_required", $"A reason of at least {ReasonMinLength} characters is required.");
        }

        if (trimmedReason.Length > ReasonMaxLength)
        {
            throw new DomainException("leave.reason_length", $"Reason cannot exceed {ReasonMaxLength} characters.");
        }

        return new LeaveRequest(
            LeaveRequestId.New(),
            employeeId,
            type,
            startDate,
            endDate,
            workdays,
            trimmedReason,
            requestedByUserId,
            requestedAtUtc);
    }

    public void Approve(Guid decidedByUserId, string decidedByName, Instant nowUtc)
    {
        EnsurePending("approve");
        SetDecision(decidedByUserId, decidedByName, nowUtc, null);
        Status = LeaveRequestStatus.Approved;
        RaiseDomainEvent(new LeaveRequestApproved(Id.Value, EmployeeId.Value, StartDate, EndDate));
    }

    public void Deny(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note)
    {
        EnsurePending("deny");
        SetDecision(decidedByUserId, decidedByName, nowUtc, note);
        Status = LeaveRequestStatus.Denied;
    }

    /// <summary>Allowed while Pending (withdrawn) or after approval (plans changed).</summary>
    public void Cancel(
        Guid decidedByUserId,
        string decidedByName,
        Instant nowUtc,
        string? note,
        LeaveCancellationReason reason)
    {
        if (Status is not (LeaveRequestStatus.Pending or LeaveRequestStatus.Approved))
        {
            throw new DomainException(
                "leave.not_cancellable", $"Only pending or approved requests can be cancelled (status: {Status}).");
        }

        // Only approved leave ever reached attendance, so only it has anything to undo.
        var wasApproved = Status == LeaveRequestStatus.Approved;

        SetDecision(decidedByUserId, decidedByName, nowUtc, note);
        Status = LeaveRequestStatus.Cancelled;
        CancellationReason = reason;

        if (wasApproved)
        {
            RaiseDomainEvent(new LeaveRequestCancelled(Id.Value, EmployeeId.Value));
        }
    }

    /// <summary>
    /// Termination closes out anything still awaiting a decision — nobody is left with the
    /// authority to approve it, so leaving it Pending would strand the row forever.
    /// Already-decided requests keep their decision as the historical record.
    /// </summary>
    public void CancelForTermination(Instant nowUtc)
    {
        if (Status != LeaveRequestStatus.Pending)
        {
            return;
        }

        Status = LeaveRequestStatus.Cancelled;
        DecidedAtUtc = nowUtc;
        DecidedByName = SystemDecider;
        DecisionNote = "Cancelled automatically: employee terminated.";
    }

    /// <summary>True when this request's date range overlaps the given inclusive range.</summary>
    public bool Overlaps(LocalDate startDate, LocalDate endDate) =>
        StartDate <= endDate && startDate <= EndDate;

    // Workweek hardcoded to Mon–Fri; lift into AttendancePolicy when the
    // office's working days actually vary (Saturday shifts, etc.).
    public static IEnumerable<LocalDate> Workdays(LocalDate startDate, LocalDate endDate)
    {
        for (var date = startDate; date <= endDate; date = date.PlusDays(1))
        {
            if (date.DayOfWeek is not (IsoDayOfWeek.Saturday or IsoDayOfWeek.Sunday))
            {
                yield return date;
            }
        }
    }

    public static int CountWorkdays(LocalDate startDate, LocalDate endDate) =>
        Workdays(startDate, endDate).Count();

    private void EnsurePending(string action)
    {
        if (Status != LeaveRequestStatus.Pending)
        {
            throw new DomainException(
                "leave.not_pending", $"Only pending requests can be {action}d (status: {Status}).");
        }
    }

    private void SetDecision(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note)
    {
        if (decidedByUserId == Guid.Empty)
        {
            throw new DomainException("leave.decided_by", "Decisions require an authenticated user.");
        }

        if (string.IsNullOrWhiteSpace(decidedByName))
        {
            throw new DomainException("leave.decided_by_name", "Decisions require the decider's display name.");
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > DecisionNoteMaxLength })
        {
            throw new DomainException(
                "leave.note_length", $"Decision note cannot exceed {DecisionNoteMaxLength} characters.");
        }

        DecidedByUserId = decidedByUserId;
        DecidedByName = decidedByName.Trim();
        DecidedAtUtc = nowUtc;
        DecisionNote = trimmedNote;
    }
}
