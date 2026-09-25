using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using NodaTime;
using Wolverine;
using Wolverine.Attributes;

namespace Erp.UseCases.Attendance.UpdateAttendancePolicy;

public static class UpdateAttendancePolicyHandler
{
    [Transactional]
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

        // Snapshot pre-change values BEFORE applying the new ones — this only builds the
        // history object in memory, it isn't persisted yet.
        var history = AttendancePolicyHistory.Snapshot(policy, command.ChangedByUserId, now);

        // Domain-level validation errors (shift window, negative grace, bad time zone)
        // are thrown as DomainException and left to bubble up to the global
        // exception handler, same as UpdateAttendanceLogHandler does for its aggregate.
        // Must not throw AFTER any persistence below, or the audit trail would record
        // a change that never actually took effect.
        policy.Update(
            shiftStart,
            shiftEnd,
            command.ClockInGraceMinutes,
            command.ClockOutGraceMinutes,
            command.TimeZoneId,
            command.MaxIzinHours,
            command.ChangedByUserId,
            now);

        // Validation passed — now persist both. [Transactional] wraps the handler in a single
        // EF Core transaction, so these two writes (and the outboxed event below) commit or
        // roll back together. The attribute is required: UseEntityFrameworkCoreTransactions in
        // Program.cs only registers the middleware, it does not apply it to any handler.
        await policyHistories.AddAsync(history, ct);
        await policies.UpdateAsync(policy, ct);

        foreach (var domainEvent in policy.DomainEvents.OfType<AttendancePolicyUpdated>())
        {
            await bus.PublishAsync(domainEvent);
        }

        return new Result<AttendancePolicyResult>.Success(AttendancePolicyMapper.ToResult(policy));
    }
}
