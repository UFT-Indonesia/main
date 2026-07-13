using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Attendance.UpdateAttendancePolicy;

public static class UpdateAttendancePolicyHandler
{
    public static async Task<Result<AttendancePolicyResult>> Handle(
        UpdateAttendancePolicyCommand command,
        IRepository<AttendancePolicy> policies,
        IRepository<AttendancePolicyHistory> policyHistories,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (!AttendancePolicyMapper.TryParseTime(command.ShiftStart, out var shiftStart))
        {
            return new Result<AttendancePolicyResult>.Error(
                "attendance_policy.shift_start", "Shift start must be a valid HH:mm time.");
        }

        if (!AttendancePolicyMapper.TryParseTime(command.ShiftEnd, out var shiftEnd))
        {
            return new Result<AttendancePolicyResult>.Error(
                "attendance_policy.shift_end", "Shift end must be a valid HH:mm time.");
        }

        var policy = await policies.GetByIdAsync(AttendancePolicyId.Singleton, ct);
        if (policy is null)
        {
            return new Result<AttendancePolicyResult>.NotFound("Attendance policy was not found.");
        }

        var now = clock.GetCurrentInstant();

        // Record the pre-change snapshot BEFORE applying the new values.
        var history = AttendancePolicyHistory.Snapshot(policy, command.ChangedByUserId, now);
        await policyHistories.AddAsync(history, ct);

        // Domain-level validation errors (shift window, negative grace, bad time zone)
        // are thrown as DomainException and left to bubble up to the global
        // exception handler, same as UpdateAttendanceLogHandler does for its aggregate.
        policy.Update(
            shiftStart,
            shiftEnd,
            command.ClockInGraceMinutes,
            command.ClockOutGraceMinutes,
            command.TimeZoneId,
            command.ChangedByUserId,
            now);

        await policies.UpdateAsync(policy, ct);

        foreach (var domainEvent in policy.DomainEvents.OfType<AttendancePolicyUpdated>())
        {
            await bus.PublishAsync(domainEvent);
        }

        return new Result<AttendancePolicyResult>.Success(AttendancePolicyMapper.ToResult(policy));
    }
}
