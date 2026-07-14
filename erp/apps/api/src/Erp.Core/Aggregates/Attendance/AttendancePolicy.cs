using Erp.Core.Aggregates.Attendance.Events;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance;

/// <summary>
/// Single global shift/grace-period policy used to derive <see cref="AttendanceDay"/>
/// status. Exactly one row exists in the system, at the fixed
/// <see cref="AttendancePolicyId.Singleton"/> id — no per-employee/per-shift policies.
/// </summary>
public sealed class AttendancePolicy : AggregateRoot<AttendancePolicyId>
{
    // EF Core constructor.
    private AttendancePolicy() { }

    private AttendancePolicy(
        AttendancePolicyId id,
        LocalTime shiftStart,
        LocalTime shiftEnd,
        int clockInGraceMinutes,
        int clockOutGraceMinutes,
        string timeZoneId,
        Guid updatedByUserId,
        Instant updatedAtUtc)
        : base(id)
    {
        ShiftStart = shiftStart;
        ShiftEnd = shiftEnd;
        ClockInGraceMinutes = clockInGraceMinutes;
        ClockOutGraceMinutes = clockOutGraceMinutes;
        TimeZoneId = timeZoneId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public LocalTime ShiftStart { get; private set; }

    public LocalTime ShiftEnd { get; private set; }

    public int ClockInGraceMinutes { get; private set; }

    public int ClockOutGraceMinutes { get; private set; }

    /// <summary>IANA time zone id (e.g. "Asia/Jakarta").</summary>
    public string TimeZoneId { get; private set; } = default!;

    public Guid UpdatedByUserId { get; private set; }

    public Instant UpdatedAtUtc { get; private set; }

    public static AttendancePolicy Create(
        LocalTime shiftStart,
        LocalTime shiftEnd,
        int clockInGraceMinutes,
        int clockOutGraceMinutes,
        string timeZoneId,
        Guid updatedByUserId,
        Instant updatedAtUtc)
    {
        EnsureValid(shiftStart, shiftEnd, clockInGraceMinutes, clockOutGraceMinutes, timeZoneId);

        return new AttendancePolicy(
            AttendancePolicyId.Singleton,
            shiftStart,
            shiftEnd,
            clockInGraceMinutes,
            clockOutGraceMinutes,
            timeZoneId,
            updatedByUserId,
            updatedAtUtc);
    }

    /// <summary>
    /// Applies new policy values. Callers that need to preserve the pre-change values
    /// (e.g. for an audit history row) must read them before calling this.
    /// </summary>
    public void Update(
        LocalTime shiftStart,
        LocalTime shiftEnd,
        int clockInGraceMinutes,
        int clockOutGraceMinutes,
        string timeZoneId,
        Guid updatedByUserId,
        Instant updatedAtUtc)
    {
        EnsureValid(shiftStart, shiftEnd, clockInGraceMinutes, clockOutGraceMinutes, timeZoneId);

        ShiftStart = shiftStart;
        ShiftEnd = shiftEnd;
        ClockInGraceMinutes = clockInGraceMinutes;
        ClockOutGraceMinutes = clockOutGraceMinutes;
        TimeZoneId = timeZoneId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = updatedAtUtc;

        RaiseDomainEvent(new AttendancePolicyUpdated(Id.Value));
    }

    /// <summary>Maps to the value type <see cref="AttendanceDay.ComputeStatus"/> consumes.</summary>
    public AttendanceDayPolicy ToAttendanceDayPolicy() => new(
        ShiftStart,
        ShiftEnd,
        ClockInGraceMinutes,
        ClockOutGraceMinutes,
        DateTimeZoneProviders.Tzdb[TimeZoneId]);

    private static void EnsureValid(
        LocalTime shiftStart,
        LocalTime shiftEnd,
        int clockInGraceMinutes,
        int clockOutGraceMinutes,
        string timeZoneId)
    {
        if (shiftStart >= shiftEnd)
        {
            throw new DomainException("attendance_policy.shift_window", "Shift start must be before shift end.");
        }

        if (clockInGraceMinutes < 0)
        {
            throw new DomainException(
                "attendance_policy.clock_in_grace", "Clock-in grace minutes must be zero or positive.");
        }

        if (clockOutGraceMinutes < 0)
        {
            throw new DomainException(
                "attendance_policy.clock_out_grace", "Clock-out grace minutes must be zero or positive.");
        }

        if (string.IsNullOrWhiteSpace(timeZoneId) || DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId) is null)
        {
            throw new DomainException(
                "attendance_policy.time_zone", "Time zone must be a valid IANA time zone id.");
        }
    }
}
