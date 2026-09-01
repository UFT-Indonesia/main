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
        int maxIzinHours,
        Guid updatedByUserId,
        Instant updatedAtUtc)
        : base(id)
    {
        ShiftStart = shiftStart;
        ShiftEnd = shiftEnd;
        ClockInGraceMinutes = clockInGraceMinutes;
        ClockOutGraceMinutes = clockOutGraceMinutes;
        TimeZoneId = timeZoneId;
        MaxIzinHours = maxIzinHours;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public LocalTime ShiftStart { get; private set; }

    public LocalTime ShiftEnd { get; private set; }

    public int ClockInGraceMinutes { get; private set; }

    public int ClockOutGraceMinutes { get; private set; }

    /// <summary>IANA time zone id (e.g. "Asia/Jakarta").</summary>
    public string TimeZoneId { get; private set; } = default!;

    /// <summary>
    /// Longest span an hourly Izin may cover, in hours. Without a cap, "hourly" Izin could
    /// span an entire side of the shift (e.g. 13:00–18:00) — a whole day off in every way that
    /// matters, filed as if it were a quick errand.
    /// </summary>
    public int MaxIzinHours { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public Instant UpdatedAtUtc { get; private set; }

    public static AttendancePolicy Create(
        LocalTime shiftStart,
        LocalTime shiftEnd,
        int clockInGraceMinutes,
        int clockOutGraceMinutes,
        string timeZoneId,
        int maxIzinHours,
        Guid updatedByUserId,
        Instant updatedAtUtc)
    {
        EnsureValid(shiftStart, shiftEnd, clockInGraceMinutes, clockOutGraceMinutes, timeZoneId, maxIzinHours);

        return new AttendancePolicy(
            AttendancePolicyId.Singleton,
            shiftStart,
            shiftEnd,
            clockInGraceMinutes,
            clockOutGraceMinutes,
            timeZoneId,
            maxIzinHours,
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
        int maxIzinHours,
        Guid updatedByUserId,
        Instant updatedAtUtc)
    {
        EnsureValid(shiftStart, shiftEnd, clockInGraceMinutes, clockOutGraceMinutes, timeZoneId, maxIzinHours);

        ShiftStart = shiftStart;
        ShiftEnd = shiftEnd;
        ClockInGraceMinutes = clockInGraceMinutes;
        ClockOutGraceMinutes = clockOutGraceMinutes;
        TimeZoneId = timeZoneId;
        MaxIzinHours = maxIzinHours;
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
        MaxIzinHours,
        DateTimeZoneProviders.Tzdb[TimeZoneId]);

    private static void EnsureValid(
        LocalTime shiftStart,
        LocalTime shiftEnd,
        int clockInGraceMinutes,
        int clockOutGraceMinutes,
        string timeZoneId,
        int maxIzinHours)
    {
        if (shiftStart >= shiftEnd)
        {
            throw new DomainException("attendance_policy.shift_window", "Shift start must be before shift end.");
        }

        // LeaveRequest.ChargePerWorkday divides an hourly Izin's minutes by (shift length − the
        // 1-hour lunch it assumes at 12:00–13:00). A shift of 60 minutes or less makes that
        // divisor zero or negative — decimal division by zero throws, and a negative divisor
        // would flip an Izin's charge into topping quota back up instead of spending it.
        var shiftMinutes = (shiftEnd.Hour * 60 + shiftEnd.Minute) - (shiftStart.Hour * 60 + shiftStart.Minute);
        if (shiftMinutes <= 60)
        {
            throw new DomainException(
                "attendance_policy.shift_too_short",
                "Shift must be longer than 60 minutes, to leave room for the assumed 1-hour lunch.");
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

        if (maxIzinHours <= 0)
        {
            throw new DomainException(
                "attendance_policy.max_izin_hours", "Max Izin hours must be positive.");
        }
    }
}
