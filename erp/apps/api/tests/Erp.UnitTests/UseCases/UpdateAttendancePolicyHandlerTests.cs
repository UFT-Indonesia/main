using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Attendance.UpdateAttendancePolicy;
using FluentAssertions;
using NodaTime;
using NSubstitute;
using Wolverine;

namespace Erp.UnitTests.UseCases;

public class UpdateAttendancePolicyHandlerTests
{
    private readonly IRepository<AttendancePolicy> _policies = Substitute.For<IRepository<AttendancePolicy>>();
    private readonly IRepository<AttendancePolicyHistory> _histories = Substitute.For<IRepository<AttendancePolicyHistory>>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    private static AttendancePolicy ExistingPolicy() => AttendancePolicy.Create(
        new LocalTime(9, 0),
        new LocalTime(18, 0),
        clockInGraceMinutes: 5,
        clockOutGraceMinutes: 5,
        "Asia/Jakarta",
        Guid.Empty,
        Instant.FromUtc(2026, 1, 1, 0, 0));

    [Fact]
    public async Task Handle_returns_not_found_when_policy_row_missing()
    {
        _policies.GetByIdAsync(Erp.SharedKernel.Identity.AttendancePolicyId.Singleton, Arg.Any<CancellationToken>())
            .Returns((AttendancePolicy?)null);

        var result = await UpdateAttendancePolicyHandler.Handle(
            new UpdateAttendancePolicyCommand("08:00", "17:00", 5, 5, "Asia/Jakarta", Guid.NewGuid()),
            _policies,
            _histories,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendancePolicyResult>.NotFound>();
    }

    [Fact]
    public async Task Handle_returns_error_for_unparsable_shift_time()
    {
        var result = await UpdateAttendancePolicyHandler.Handle(
            new UpdateAttendancePolicyCommand("not-a-time", "17:00", 5, 5, "Asia/Jakarta", Guid.NewGuid()),
            _policies,
            _histories,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendancePolicyResult>.Error>()
            .Which.Code.Should().Be("attendance_policy.shift_start");
    }

    [Fact]
    public async Task Handle_lets_domain_validation_errors_bubble_up()
    {
        var policy = ExistingPolicy();
        _policies.GetByIdAsync(policy.Id, Arg.Any<CancellationToken>()).Returns(policy);
        _clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 1, 2, 0, 0));

        var act = () => UpdateAttendancePolicyHandler.Handle(
            new UpdateAttendancePolicyCommand("18:00", "09:00", 5, 5, "Asia/Jakarta", Guid.NewGuid()),
            _policies,
            _histories,
            _clock,
            _bus,
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Code == "attendance_policy.shift_window");
    }

    [Fact]
    public async Task Handle_does_not_persist_history_row_when_domain_validation_fails()
    {
        var policy = ExistingPolicy();
        _policies.GetByIdAsync(policy.Id, Arg.Any<CancellationToken>()).Returns(policy);
        _clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 1, 2, 0, 0));

        var act = () => UpdateAttendancePolicyHandler.Handle(
            new UpdateAttendancePolicyCommand("18:00", "09:00", 5, 5, "Asia/Jakarta", Guid.NewGuid()),
            _policies,
            _histories,
            _clock,
            _bus,
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();

        // The shift window was invalid — nothing actually changed. A history row here
        // would record a "change" that never took effect.
        await _histories.DidNotReceive().AddAsync(Arg.Any<AttendancePolicyHistory>(), Arg.Any<CancellationToken>());
        await _policies.DidNotReceive().UpdateAsync(Arg.Any<AttendancePolicy>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_writes_history_row_with_pre_change_values_before_updating()
    {
        var policy = ExistingPolicy();
        _policies.GetByIdAsync(policy.Id, Arg.Any<CancellationToken>()).Returns(policy);
        var now = Instant.FromUtc(2026, 1, 2, 0, 0);
        _clock.GetCurrentInstant().Returns(now);
        var changedBy = Guid.NewGuid();

        AttendancePolicyHistory? capturedHistory = null;
        _histories.AddAsync(Arg.Do<AttendancePolicyHistory>(h => capturedHistory = h), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<AttendancePolicyHistory>());

        var result = await UpdateAttendancePolicyHandler.Handle(
            new UpdateAttendancePolicyCommand("08:00", "17:00", 10, 10, "Asia/Makassar", changedBy),
            _policies,
            _histories,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendancePolicyResult>.Success>();

        // History captures the PRE-change values (09:00-18:00, 5/5, Jakarta) — not the new ones.
        capturedHistory.Should().NotBeNull();
        capturedHistory!.ShiftStart.Should().Be(new LocalTime(9, 0));
        capturedHistory.ShiftEnd.Should().Be(new LocalTime(18, 0));
        capturedHistory.ClockInGraceMinutes.Should().Be(5);
        capturedHistory.ClockOutGraceMinutes.Should().Be(5);
        capturedHistory.TimeZoneId.Should().Be("Asia/Jakarta");
        capturedHistory.ChangedByUserId.Should().Be(changedBy);
        capturedHistory.ChangedAtUtc.Should().Be(now);

        // The aggregate itself now holds the NEW values.
        policy.ShiftStart.Should().Be(new LocalTime(8, 0));
        policy.TimeZoneId.Should().Be("Asia/Makassar");

        await _policies.Received(1).UpdateAsync(policy, Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(Arg.Any<AttendancePolicyUpdated>());
    }
}
