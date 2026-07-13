using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Aggregates.Employees;
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

    /// <summary>Calendar day in the shift time zone (Asia/Jakarta).</summary>
    public LocalDate CalendarDate { get; private set; }

    /// <summary>UTC instant of the day's first punch.</summary>
    public Instant? TapInUtc { get; private set; }

    /// <summary>UTC instant of the day's last punch; null when only one punch exists.</summary>
    public Instant? TapOutUtc { get; private set; }

    public AttendanceDayStatus Status { get; private set; }

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

    public void Recompute(IReadOnlyList<AttendanceLog> punchesForDay, AttendanceDayPolicy policy)
    {
        EnsurePunches(punchesForDay);
        Apply(punchesForDay, policy, force: false);
    }

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
