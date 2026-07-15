using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Leave;

/// <summary>
/// A full-day leave request entered on behalf of an employee (no self-service yet —
/// employees have no login accounts). Lifecycle:
/// Pending → Approved | Denied | Cancelled, and Approved → Cancelled.
/// Denied/Cancelled are terminal; wrong dates are fixed by cancel + resubmit, never edit.
/// </summary>
public sealed class LeaveRequest : AggregateRoot<LeaveRequestId>
{
    public const int ReasonMaxLength = 500;
    public const int DecisionNoteMaxLength = 500;

    // EF Core constructor.
    private LeaveRequest() { }

    private LeaveRequest(
        LeaveRequestId id,
        EmployeeId employeeId,
        LeaveType type,
        LocalDate startDate,
        LocalDate endDate,
        int workdayCount,
        string? reason,
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

    public string? Reason { get; private set; }

    public LeaveRequestStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public Instant RequestedAtUtc { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    /// <summary>Display-name snapshot of the decider, so the trail survives renames.</summary>
    public string? DecidedByName { get; private set; }

    public Instant? DecidedAtUtc { get; private set; }

    /// <summary>Optional note recorded on deny/cancel.</summary>
    public string? DecisionNote { get; private set; }

    public static LeaveRequest Create(
        EmployeeId employeeId,
        LeaveType type,
        LocalDate startDate,
        LocalDate endDate,
        string? reason,
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

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmedReason is { Length: > ReasonMaxLength })
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
    }

    public void Deny(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note)
    {
        EnsurePending("deny");
        SetDecision(decidedByUserId, decidedByName, nowUtc, note);
        Status = LeaveRequestStatus.Denied;
    }

    /// <summary>Allowed while Pending (withdrawn) or after approval (plans changed).</summary>
    public void Cancel(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note)
    {
        if (Status is not (LeaveRequestStatus.Pending or LeaveRequestStatus.Approved))
        {
            throw new DomainException(
                "leave.not_cancellable", $"Only pending or approved requests can be cancelled (status: {Status}).");
        }

        SetDecision(decidedByUserId, decidedByName, nowUtc, note);
        Status = LeaveRequestStatus.Cancelled;
    }

    /// <summary>True when this request's date range overlaps the given inclusive range.</summary>
    public bool Overlaps(LocalDate startDate, LocalDate endDate) =>
        StartDate <= endDate && startDate <= EndDate;

    // ponytail: workweek hardcoded to Mon–Fri; lift into AttendancePolicy when the
    // office's working days actually vary (Saturday shifts, etc.).
    public static int CountWorkdays(LocalDate startDate, LocalDate endDate)
    {
        var count = 0;
        for (var date = startDate; date <= endDate; date = date.PlusDays(1))
        {
            if (date.DayOfWeek is not (IsoDayOfWeek.Saturday or IsoDayOfWeek.Sunday))
            {
                count++;
            }
        }

        return count;
    }

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
