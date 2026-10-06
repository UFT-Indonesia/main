using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Overtime;

/// <summary>
/// An Owner/Manager telling one employee to work overtime on one date, from–to. The window sits
/// inside 05:00 on <see cref="Date"/> → 05:00 the next day and counts on its start date, so a
/// 18:30 → 01:00 window is one day's overtime. <see cref="IsDayOff"/> is snapshotted at creation
/// and never re-read from the holiday calendar (GSS08 decision 12).
/// </summary>
public sealed class OvertimeAssignment : AggregateRoot<OvertimeAssignmentId>
{
    public const int DecisionNoteMaxLength = 500;

    /// <summary>Weekday OT starts here: 18:00–18:30 is a break after the shift. <see cref="Attendance.AttendancePolicy.MaxShiftEnd"/> keeps the shift from reaching it.</summary>
    public static readonly LocalTime WeekdayStart = new(18, 30);

    /// <summary>Where one overtime day ends and the next begins.</summary>
    public static readonly LocalTime DayBoundary = new(5, 0);

    // EF Core constructor.
    private OvertimeAssignment() { }

    private OvertimeAssignment(
        OvertimeAssignmentId id, EmployeeId employeeId, LocalDate date, LocalTime start, LocalTime end,
        bool isDayOff, Guid requestedByUserId, Instant requestedAtUtc)
        : base(id)
    {
        EmployeeId = employeeId;
        Date = date;
        StartTime = start;
        EndTime = end;
        IsDayOff = isDayOff;
        Status = OvertimeStatus.Pending;
        RequestedByUserId = requestedByUserId;
        RequestedAtUtc = requestedAtUtc;
    }

    public EmployeeId EmployeeId { get; private set; }

    // EF Core navigation — read-only, not part of domain behavior.
    public Employee? Employee { get; private set; }

    /// <summary>The start date. A window past midnight still belongs to this date.</summary>
    public LocalDate Date { get; private set; }

    public LocalTime StartTime { get; private set; }

    /// <summary>On the next day when not after <see cref="StartTime"/>.</summary>
    public LocalTime EndTime { get; private set; }

    public bool IsDayOff { get; private set; }

    public OvertimeStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public Instant RequestedAtUtc { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public string? DecidedByName { get; private set; }

    public Instant? DecidedAtUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    /// <summary>Counted hours / pay frozen at period close; null while the period is open.</summary>
    public int? FrozenHours { get; private set; }

    public decimal? FrozenAmount { get; private set; }

    public Instant? FrozenAtUtc { get; private set; }

    public bool IsFrozen => FrozenAtUtc is not null;

    /// <summary>Pending or Approved: the date is spoken for, so no leave may be filed on it.</summary>
    public bool IsLive => Status is OvertimeStatus.Pending or OvertimeStatus.Approved;

    /// <summary>Whether this assignment claims the punches inside its window.</summary>
    public bool OwnsPunches => Status is OvertimeStatus.Pending or OvertimeStatus.Approved or OvertimeStatus.Expired;

    public LocalDate EndDate => EndTime > StartTime ? Date : Date.PlusDays(1);

    public Instant StartAt(DateTimeZone zone) => Date.At(StartTime).InZoneLeniently(zone).ToInstant();

    public Instant EndAt(DateTimeZone zone) => EndDate.At(EndTime).InZoneLeniently(zone).ToInstant();

    public static OvertimeAssignment Create(
        EmployeeId employeeId, LocalDate date, LocalTime? start, LocalTime end, bool isDayOff,
        Guid requestedByUserId, string requestedByName, Instant nowUtc, bool autoApprove)
    {
        if (employeeId == EmployeeId.Empty)
        {
            throw new DomainException("overtime.employee_id", "Employee id is required.");
        }

        if (requestedByUserId == Guid.Empty)
        {
            throw new DomainException("overtime.requested_by", "Overtime requires an authenticated requester.");
        }

        var resolvedStart = isDayOff
            ? start ?? throw new DomainException("overtime.start_required", "A day-off overtime needs a start time.")
            : WeekdayStart;
        if (!isDayOff && start is { } given && given != WeekdayStart)
        {
            throw new DomainException(
                "overtime.start_fixed", $"Weekday overtime always starts at {WeekdayStart:HH:mm}.");
        }

        EnsureWindow(isDayOff, resolvedStart, end);

        var assignment = new OvertimeAssignment(
            OvertimeAssignmentId.New(), employeeId, date, resolvedStart, end, isDayOff, requestedByUserId, nowUtc);
        if (autoApprove)
        {
            assignment.Approve(requestedByUserId, requestedByName, nowUtc);
        }

        return assignment;
    }

    /// <summary>Length of a window in minutes, reading an end that is not after the start as next-day.</summary>
    public static int WindowMinutes(LocalTime start, LocalTime end)
    {
        var minutes = (end.Hour * 60 + end.Minute) - (start.Hour * 60 + start.Minute);
        return minutes > 0 ? minutes : minutes + 24 * 60;
    }

    private static void EnsureWindow(bool isDayOff, LocalTime start, LocalTime end)
    {
        if (start == end)
        {
            throw new DomainException("overtime.window", "Overtime must end after it starts.");
        }

        if (isDayOff && start < DayBoundary)
        {
            throw new DomainException(
                "overtime.start_before_boundary", $"Overtime cannot start before {DayBoundary:HH:mm}.");
        }

        // Ends later the same day, or runs into the small hours — never past the 05:00 boundary.
        if (end < start && end > DayBoundary)
        {
            throw new DomainException(
                "overtime.end_after_boundary", $"An overtime past midnight must end by {DayBoundary:HH:mm}.");
        }
    }

    public void Approve(Guid decidedByUserId, string decidedByName, Instant nowUtc)
    {
        EnsureStatus(OvertimeStatus.Pending, "approved");
        SetDecision(decidedByUserId, decidedByName, nowUtc, null);
        Status = OvertimeStatus.Approved;
    }

    public void Reject(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note)
    {
        EnsureStatus(OvertimeStatus.Pending, "rejected");
        SetDecision(decidedByUserId, decidedByName, nowUtc, note);
        Status = OvertimeStatus.Rejected;
    }

    /// <summary>Not allowed once the window has punches: the work happened, so it is paid by what the punches say.</summary>
    public void Cancel(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note, bool hasPunches)
    {
        EnsureEditable("cancelled");
        if (hasPunches)
        {
            throw new DomainException(
                "overtime.has_punches", "This overtime already has punches and can no longer be cancelled.");
        }

        SetDecision(decidedByUserId, decidedByName, nowUtc, note);
        Status = OvertimeStatus.Cancelled;
    }

    /// <summary>
    /// Moves the end. With punches it can only grow. <paramref name="returnToPending"/> is a
    /// Manager's edit: an Owner has to look at the new window again.
    /// </summary>
    public void ChangeEnd(LocalTime newEnd, bool hasPunches, bool returnToPending)
    {
        EnsureEditable("edited");
        EnsureWindow(IsDayOff, StartTime, newEnd);
        if (hasPunches && WindowMinutes(StartTime, newEnd) < WindowMinutes(StartTime, EndTime))
        {
            throw new DomainException(
                "overtime.shorten_with_punches", "An overtime that already has punches can only be extended.");
        }

        EndTime = newEnd;
        if (returnToPending)
        {
            Status = OvertimeStatus.Pending;
            DecidedByUserId = null;
            DecidedByName = null;
            DecidedAtUtc = null;
            DecisionNote = null;
        }
    }

    /// <summary>Period close with nobody having decided: no pay, but the punches stay claimed.</summary>
    public void Expire()
    {
        if (Status == OvertimeStatus.Pending)
        {
            Status = OvertimeStatus.Expired;
        }
    }

    public void Freeze(int hours, decimal amount, Instant nowUtc)
    {
        FrozenHours = hours;
        FrozenAmount = amount;
        FrozenAtUtc = nowUtc;
    }

    private void EnsureEditable(string verb)
    {
        if (IsFrozen)
        {
            throw new DomainException("overtime.period_closed", "This overtime's period is closed.");
        }

        if (!IsLive)
        {
            throw new DomainException(
                "overtime.not_editable", $"Only pending or approved overtime can be {verb} (status: {Status}).");
        }
    }

    private void EnsureStatus(OvertimeStatus expected, string verb)
    {
        if (Status != expected)
        {
            throw new DomainException(
                "overtime.not_pending", $"Only pending overtime can be {verb} (status: {Status}).");
        }
    }

    private void SetDecision(Guid decidedByUserId, string decidedByName, Instant nowUtc, string? note)
    {
        if (decidedByUserId == Guid.Empty || string.IsNullOrWhiteSpace(decidedByName))
        {
            throw new DomainException("overtime.decided_by", "Decisions require an authenticated user.");
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed is { Length: > DecisionNoteMaxLength })
        {
            throw new DomainException(
                "overtime.note_length", $"Decision note cannot exceed {DecisionNoteMaxLength} characters.");
        }

        DecidedByUserId = decidedByUserId;
        DecidedByName = decidedByName.Trim();
        DecidedAtUtc = nowUtc;
        DecisionNote = trimmed;
    }
}
