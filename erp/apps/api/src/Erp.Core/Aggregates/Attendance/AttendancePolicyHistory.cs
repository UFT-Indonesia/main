using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance;

/// <summary>
/// Append-only audit row capturing the PRE-change values of the global
/// <see cref="AttendancePolicy"/> whenever it is updated. Not an aggregate root —
/// no domain events, nothing else mutates it. Nothing reads this back via
/// API/UI yet; it exists purely as a persisted trail.
/// </summary>
public sealed class AttendancePolicyHistory : Entity
{
    // EF Core constructor.
    private AttendancePolicyHistory() { }

    private AttendancePolicyHistory(
        Guid id,
        AttendancePolicyId policyId,
        LocalTime shiftStart,
        LocalTime shiftEnd,
        int clockInGraceMinutes,
        int clockOutGraceMinutes,
        string timeZoneId,
        Guid changedByUserId,
        Instant changedAtUtc)
        : base(id)
    {
        PolicyId = policyId;
        ShiftStart = shiftStart;
        ShiftEnd = shiftEnd;
        ClockInGraceMinutes = clockInGraceMinutes;
        ClockOutGraceMinutes = clockOutGraceMinutes;
        TimeZoneId = timeZoneId;
        ChangedByUserId = changedByUserId;
        ChangedAtUtc = changedAtUtc;
    }

    public AttendancePolicyId PolicyId { get; private set; }

    public LocalTime ShiftStart { get; private set; }

    public LocalTime ShiftEnd { get; private set; }

    public int ClockInGraceMinutes { get; private set; }

    public int ClockOutGraceMinutes { get; private set; }

    public string TimeZoneId { get; private set; } = default!;

    public Guid ChangedByUserId { get; private set; }

    public Instant ChangedAtUtc { get; private set; }

    /// <summary>Snapshots the CURRENT (pre-change) values of a policy, before it is updated.</summary>
    public static AttendancePolicyHistory Snapshot(
        AttendancePolicy policy, Guid changedByUserId, Instant changedAtUtc) =>
        new(
            Guid.NewGuid(),
            policy.Id,
            policy.ShiftStart,
            policy.ShiftEnd,
            policy.ClockInGraceMinutes,
            policy.ClockOutGraceMinutes,
            policy.TimeZoneId,
            changedByUserId,
            changedAtUtc);
}
