using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance;

/// <summary>
/// Materialized employee-day view over raw punches: one row per employee per
/// calendar day (in the configured shift time zone). Tap-In is the day's first
/// punch, Tap-Out the day's last punch (only when more than one punch exists).
/// Status is derived from the configurable shift grace windows.
/// <para>
/// Approved leave is the one thing that materializes a row without punches (see
/// <see cref="CreateForLeave"/>). Punches still win the status on a day leave covers — the
/// employee demonstrably worked — but <see cref="LeaveRequestId"/> stays put so the day
/// remains attributable to the leave that covered it.
/// </para>
/// </summary>
public sealed class AttendanceDay : AggregateRoot<AttendanceDayId>
{
    // EF Core constructor.
    private AttendanceDay() { }

    private AttendanceDay(
        AttendanceDayId id,
        EmployeeId employeeId,
        LocalDate calendarDate)
        : base(id)
    {
        EmployeeId = employeeId;
        CalendarDate = calendarDate;
    }

    public EmployeeId EmployeeId { get; private set; }

    // EF Core navigation — read-only, not part of domain behavior.
    public Employee? Employee { get; private set; }

    /// <summary>Calendar day in the configured shift time zone.</summary>
    public LocalDate CalendarDate { get; private set; }

    /// <summary>UTC instant of the day's first punch.</summary>
    public Instant? TapInUtc { get; private set; }

    /// <summary>UTC instant of the day's last punch; null when only one punch exists.</summary>
    public Instant? TapOutUtc { get; private set; }

    public AttendanceDayStatus Status { get; private set; }

    /// <summary>The approved leave covering this day, when one does.</summary>
    public LeaveRequestId? LeaveRequestId { get; private set; }

    // EF Core navigation — read-only, not part of domain behavior.
    public LeaveRequest? LeaveRequest { get; private set; }

    public static AttendanceDay Create(
        EmployeeId employeeId,
        LocalDate calendarDate,
        IReadOnlyList<AttendanceLog> punchesForDay,
        AttendanceDayPolicy policy)
    {
        if (employeeId == EmployeeId.Empty)
        {
            throw new DomainException("attendance_day.employee_id", "Employee id is required.");
        }

        EnsurePunches(punchesForDay);

        var day = new AttendanceDay(AttendanceDayId.New(), employeeId, calendarDate);
        day.Apply(punchesForDay, policy, force: true);
        return day;
    }

    /// <summary>
    /// A day the employee is on approved leave for and has not punched on. No punches means
    /// no shift windows to judge, so the status is stated rather than computed.
    /// </summary>
    public static AttendanceDay CreateForLeave(
        EmployeeId employeeId,
        LocalDate calendarDate,
        LeaveRequestId leaveRequestId)
    {
        if (employeeId == EmployeeId.Empty)
        {
            throw new DomainException("attendance_day.employee_id", "Employee id is required.");
        }

        // `default` rather than LeaveRequestId.Empty: inside this type the name resolves to
        // the property, not the struct.
        if (leaveRequestId == default)
        {
            throw new DomainException("attendance_day.leave_request_id", "Leave request id is required.");
        }

        return new AttendanceDay(AttendanceDayId.New(), employeeId, calendarDate)
        {
            Status = AttendanceDayStatus.OnLeave,
            LeaveRequestId = leaveRequestId,
        };
    }

    public void Recompute(IReadOnlyList<AttendanceLog> punchesForDay, AttendanceDayPolicy policy)
    {
        EnsurePunches(punchesForDay);
        Apply(punchesForDay, policy, force: false);
    }

    /// <summary>
    /// The day's last punch was moved away or deleted, but leave still covers it — fall back
    /// to the leave view instead of dropping a day the employee is legitimately away for.
    /// </summary>
    public void RevertToLeave()
    {
        if (LeaveRequestId is null)
        {
            throw new DomainException(
                "attendance_day.no_leave_link", "This day is not covered by a leave request.");
        }

        TapInUtc = null;
        TapOutUtc = null;
        Status = AttendanceDayStatus.OnLeave;
    }

    /// <summary>
    /// The covering leave was cancelled. The row itself survives only if punches justify it;
    /// the caller deletes it otherwise (see the LeaveRequestCancelled handler).
    /// </summary>
    public void ClearLeaveLink() => LeaveRequestId = null;

    private static void EnsurePunches(IReadOnlyList<AttendanceLog> punchesForDay)
    {
        if (punchesForDay is not { Count: > 0 })
        {
            throw new DomainException(
                "attendance_day.no_punches",
                "An attendance day requires at least one punch.");
        }
    }

    private void Apply(IReadOnlyList<AttendanceLog> punchesForDay, AttendanceDayPolicy policy, bool force)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var tapIn = punchesForDay.Min(punch => punch.PunchedAtUtc);
        Instant? tapOut = punchesForDay.Count > 1
            ? punchesForDay.Max(punch => punch.PunchedAtUtc)
            : null;
        var status = ComputeStatus(tapIn, tapOut, policy);

        if (!force && TapInUtc == tapIn && TapOutUtc == tapOut && Status == status)
        {
            return;
        }

        TapInUtc = tapIn;
        TapOutUtc = tapOut;
        Status = status;

        RaiseDomainEvent(new AttendanceDayRecomputed(
            Id.Value,
            EmployeeId.Value,
            CalendarDate,
            TapInUtc,
            TapOutUtc,
            Status));
    }

    private AttendanceDayStatus ComputeStatus(Instant tapIn, Instant? tapOut, AttendanceDayPolicy policy)
    {
        if (tapOut is null)
        {
            return AttendanceDayStatus.Incomplete;
        }

        var latestAllowedTapIn = CalendarDate
            .At(policy.ShiftStart)
            .InZoneLeniently(policy.TimeZone)
            .ToInstant()
            .Plus(Duration.FromMinutes(policy.ClockInGraceMinutes));

        var earliestAllowedTapOut = CalendarDate
            .At(policy.ShiftEnd)
            .InZoneLeniently(policy.TimeZone)
            .ToInstant()
            .Minus(Duration.FromMinutes(policy.ClockOutGraceMinutes));

        return tapIn <= latestAllowedTapIn && tapOut.Value >= earliestAllowedTapOut
            ? AttendanceDayStatus.Complete
            : AttendanceDayStatus.Incomplete;
    }
}
