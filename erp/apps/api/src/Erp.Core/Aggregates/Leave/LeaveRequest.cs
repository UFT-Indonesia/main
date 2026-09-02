using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave.Events;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.Core.Aggregates.Attendance;
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

    /// <summary>Cap on the doctor's note a Sick request carries. Comfortable for a scan or a phone photo.</summary>
    public const long AttachmentMaxBytes = 10 * 1024 * 1024;

    /// <summary>What a doctor's note may be: a scanned document, or a photo of one.</summary>
    public static readonly IReadOnlyCollection<string> AllowedAttachmentContentTypes =
        ["application/pdf", "image/jpeg", "image/png"];

    /// <summary>Stands in for a human decider on decisions the system makes on its own.</summary>
    public const string SystemDecider = "System";

    /// <summary>
    /// The lunch break every partial request is measured against: a half-day's Morning/Afternoon
    /// split, and the "must stay on one side" rule for hourly Izin. Fixed rather than read from
    /// <see cref="AttendancePolicy"/>, which has no lunch field —
    /// only shift start/end.
    /// </summary>
    public static readonly LocalTime LunchStart = new(12, 0);
    public static readonly LocalTime LunchEnd = new(13, 0);

    /// <summary>The hour values <see cref="StartHour"/>/<see cref="EndHour"/> may take. 12 excluded.</summary>
    public static readonly IReadOnlyCollection<int> AllowedHourlyBoundaries = [9, 10, 11, 13, 14, 15, 16, 17, 18];

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
        LeaveAttachment? attachment,
        bool halfDay,
        HalfDayPeriod? halfDayPeriod,
        int? startHour,
        int? endHour,
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
        Attachment = attachment;
        HalfDay = halfDay;
        Period = halfDayPeriod;
        StartHour = startHour;
        EndHour = endHour;
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

    /// <summary>
    /// The supporting document, required on Sick and rejected on every other type — see
    /// <see cref="Create"/>. Null on requests filed before attachments existed.
    /// </summary>
    public LeaveAttachment? Attachment { get; private set; }

    /// <summary>Half-day, Annual only. When true, <see cref="HalfDayPeriod"/> says which half.</summary>
    public bool HalfDay { get; private set; }

    public HalfDayPeriod? Period { get; private set; }

    /// <summary>
    /// Whole-hour bounds of an hourly Izin, both from <see cref="AllowedHourlyBoundaries"/> and
    /// on the same side of the lunch hour. Null on every request that isn't hourly Izin.
    /// </summary>
    public int? StartHour { get; private set; }

    public int? EndHour { get; private set; }

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

    /// <summary>
    /// Set by <see cref="Edit"/>, all five together. Only the most recent edit is kept — the
    /// question worth answering is "who moved this, and from what dates", not the full history.
    /// Null on a request nobody has edited.
    /// </summary>
    public Guid? EditedByUserId { get; private set; }

    public string? EditedByName { get; private set; }

    public Instant? EditedAtUtc { get; private set; }

    public LocalDate? PreviousStartDate { get; private set; }

    public LocalDate? PreviousEndDate { get; private set; }

    public static LeaveRequest Create(
        EmployeeId employeeId,
        LeaveType type,
        LocalDate startDate,
        LocalDate endDate,
        string reason,
        LeaveAttachment? attachment,
        bool halfDay,
        HalfDayPeriod? halfDayPeriod,
        int? startHour,
        int? endHour,
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

        var workdays = EnsureShapeValid(
            type, startDate, endDate, halfDay, halfDayPeriod, startHour, endHour);

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

        // Sick leave is the only type anyone has to prove; carrying a file on the others would
        // be storing a document nobody asked for and nobody will read.
        if (type == LeaveType.Sick && attachment is null)
        {
            throw new DomainException(
                "leave.attachment_required", "Sick leave requires a supporting document.");
        }

        if (type != LeaveType.Sick && attachment is not null)
        {
            throw new DomainException(
                "leave.attachment_not_allowed", $"{type} leave does not take a supporting document.");
        }

        return new LeaveRequest(
            LeaveRequestId.New(),
            employeeId,
            type,
            startDate,
            endDate,
            workdays,
            trimmedReason,
            attachment,
            halfDay,
            halfDayPeriod,
            startHour,
            endHour,
            requestedByUserId,
            requestedAtUtc);
    }

    /// <summary>
    /// Everything that makes a date range and a half-day/hourly shape legal, shared by
    /// <see cref="Create"/> and <see cref="Edit"/> so the two can never drift apart — an edit
    /// has to clear exactly the bar a new request does. Returns the workday count.
    /// </summary>
    private static int EnsureShapeValid(
        LeaveType type,
        LocalDate startDate,
        LocalDate endDate,
        bool halfDay,
        HalfDayPeriod? halfDayPeriod,
        int? startHour,
        int? endHour)
    {
        if (startDate > endDate)
        {
            throw new DomainException("leave.date_range", "Start date must be on or before end date.");
        }

        var workdays = CountWorkdays(startDate, endDate);
        if (workdays == 0)
        {
            throw new DomainException("leave.no_workdays", "Leave range contains no working days (Mon–Fri).");
        }

        // Half-day is Annual's own toggle; every other type must leave both fields alone.
        if (halfDay && type != LeaveType.Annual)
        {
            throw new DomainException(
                "leave.half_day_not_allowed", $"{type} leave cannot be filed as a half day.");
        }

        if (halfDay != halfDayPeriod.HasValue)
        {
            throw new DomainException(
                "leave.half_day_period", "A half-day request requires choosing Morning or Afternoon.");
        }

        // Hourly is Izin's own toggle; every other type must leave both hours alone.
        var hourly = startHour.HasValue || endHour.HasValue;
        if (hourly && type != LeaveType.Permission)
        {
            throw new DomainException(
                "leave.hourly_not_allowed", $"{type} leave cannot be filed as hourly.");
        }

        if (hourly)
        {
            // The same clock-hour range is charged on every workday in [startDate, endDate] —
            // see ChargePerWorkday.
            if (startHour is not { } sh || endHour is not { } eh)
            {
                throw new DomainException(
                    "leave.hourly_range_incomplete", "Hourly Izin requires both a start and an end hour.");
            }

            if (!AllowedHourlyBoundaries.Contains(sh) || !AllowedHourlyBoundaries.Contains(eh))
            {
                throw new DomainException(
                    "leave.hourly_range_invalid", "Start and end hour must be whole hours between 09:00 and " +
                    "18:00, excluding the 12:00 lunch hour.");
            }

            if (sh >= eh)
            {
                throw new DomainException(
                    "leave.hourly_range_invalid", "Start hour must be before end hour.");
            }

            var bothMorning = sh < LunchStart.Hour && eh <= LunchStart.Hour;
            var bothAfternoon = sh >= LunchEnd.Hour && eh >= LunchEnd.Hour;
            if (!bothMorning && !bothAfternoon)
            {
                throw new DomainException(
                    "leave.hourly_range_crosses_lunch",
                    "Start and end hour must both fall before or both fall after the lunch hour.");
            }
        }

        return workdays;
    }

    /// <summary>
    /// Moves an existing request's dates and half-day/hourly shape. Type, reason and attachment
    /// are deliberately not editable — changing those makes it a different absence, which is
    /// what cancel-and-refile is for.
    /// <para>
    /// Only the shape changes here. Whether the edit also decides a Pending request is the
    /// caller's call (see EditLeaveRequestHandler) — the domain has no view on who outranks whom.
    /// Downstream attendance rows are the caller's job too: the dates this was materialized
    /// against have just moved.
    /// </para>
    /// </summary>
    public void Edit(
        LocalDate startDate,
        LocalDate endDate,
        bool halfDay,
        HalfDayPeriod? halfDayPeriod,
        int? startHour,
        int? endHour,
        Guid editedByUserId,
        string editedByName,
        Instant nowUtc)
    {
        if (Status is not (LeaveRequestStatus.Pending or LeaveRequestStatus.Approved))
        {
            throw new DomainException(
                "leave.not_editable", $"Only pending or approved requests can be edited (status: {Status}).");
        }

        if (editedByUserId == Guid.Empty)
        {
            throw new DomainException("leave.edited_by", "Edits require an authenticated user.");
        }

        if (string.IsNullOrWhiteSpace(editedByName))
        {
            throw new DomainException("leave.edited_by_name", "Edits require the editor's display name.");
        }

        var workdays = EnsureShapeValid(
            Type, startDate, endDate, halfDay, halfDayPeriod, startHour, endHour);

        // Only the most recent edit is kept — enough to answer "who moved my leave, and from
        // when?", which is the question this exists for.
        PreviousStartDate = StartDate;
        PreviousEndDate = EndDate;

        StartDate = startDate;
        EndDate = endDate;
        WorkdayCount = workdays;
        HalfDay = halfDay;
        Period = halfDayPeriod;
        StartHour = startHour;
        EndHour = endHour;

        EditedByUserId = editedByUserId;
        EditedByName = editedByName.Trim();
        EditedAtUtc = nowUtc;
    }

    public void Approve(Guid decidedByUserId, string decidedByName, Instant nowUtc)
    {
        EnsurePending("approve");
        SetDecision(decidedByUserId, decidedByName, nowUtc, null);
        Status = LeaveRequestStatus.Approved;
        RaiseDomainEvent(new LeaveRequestApproved(
            Id.Value, EmployeeId.Value, StartDate, EndDate, IsFractional: HalfDay || StartHour is not null));
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

    /// <summary>
    /// The hours of the shift this request actually occupies, once approved. A plain full-day
    /// request (Sick, Unpaid, non-half Annual, non-hourly Izin) occupies the whole shift; a
    /// half-day occupies whichever side of lunch it named; an hourly Izin occupies exactly its
    /// own range. Two approved requests conflict only when these windows actually intersect —
    /// see ApprovedLeaveOverlappingSpec's use in CreateLeaveRequestHandler.
    /// </summary>
    public (LocalTime Start, LocalTime End) OccupiedWindow(AttendanceDayPolicy policy) =>
        OccupiedWindow(HalfDay, Period, StartHour, EndHour, policy);

    /// <summary>
    /// Static twin of the instance method, for checking a candidate request against approved
    /// ones before it has been constructed — see its use in CreateLeaveRequestHandler.
    /// </summary>
    public static (LocalTime Start, LocalTime End) OccupiedWindow(
        bool halfDay, HalfDayPeriod? period, int? startHour, int? endHour, AttendanceDayPolicy policy)
    {
        if (startHour is { } sh && endHour is { } eh)
        {
            return (new LocalTime(sh, 0), new LocalTime(eh, 0));
        }

        if (halfDay)
        {
            return period == HalfDayPeriod.Morning
                ? (policy.ShiftStart, LunchStart)
                : (LunchEnd, policy.ShiftEnd);
        }

        return (policy.ShiftStart, policy.ShiftEnd);
    }

    /// <summary>True when two occupied windows share any time at all.</summary>
    public static bool WindowsIntersect(
        (LocalTime Start, LocalTime End) a, (LocalTime Start, LocalTime End) b) =>
        a.Start < b.End && b.Start < a.End;

    /// <summary>
    /// Quota this request spends for each workday it covers: 1 for a plain request, 0.5 for a
    /// half day, or the hourly range as a fraction of a net working day (shift length minus the
    /// one-hour lunch — lunch isn't work time, so a full day of hours taken is a full day charged).
    /// </summary>
    public decimal ChargePerWorkday(AttendanceDayPolicy policy) =>
        ChargePerWorkday(HalfDay, StartHour, EndHour, policy);

    /// <summary>
    /// Static so <c>LeaveQuotaGuard</c> can price a request before one is ever constructed —
    /// the fast, pre-creation quota check runs on raw form fields, not a built aggregate.
    /// </summary>
    public static decimal ChargePerWorkday(
        bool halfDay, int? startHour, int? endHour, AttendanceDayPolicy policy)
    {
        if (halfDay)
        {
            return 0.5m;
        }

        if (startHour is { } sh && endHour is { } eh)
        {
            var netMinutes = MinutesOfDay(policy.ShiftEnd) - MinutesOfDay(policy.ShiftStart) - 60;
            var takenMinutes = (eh - sh) * 60;
            return (decimal)takenMinutes / netMinutes;
        }

        return 1m;
    }

    /// <summary>Total quota this request spends across every workday it covers, once approved.</summary>
    public decimal TotalCharge(AttendanceDayPolicy policy) => WorkdayCount * ChargePerWorkday(policy);

    private static int MinutesOfDay(LocalTime time) => time.Hour * 60 + time.Minute;

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
