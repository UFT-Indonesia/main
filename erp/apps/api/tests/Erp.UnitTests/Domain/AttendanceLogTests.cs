using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.SharedKernel.Domain.Errors;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.Domain;

public class AttendanceLogTests
{
    private static readonly Instant Now = Instant.FromUtc(2025, 5, 12, 2, 0); // 09:00 WIB

    [Fact]
    public void FromDevice_records_log_and_event()
    {
        var employeeId = Guid.NewGuid();

        var log = AttendanceLog.FromDevice(employeeId, Now, PunchType.In, "esp32-1");

        log.EmployeeId.Should().Be(employeeId);
        log.Source.Should().Be(AttendanceSource.Device);
        log.PunchType.Should().Be(PunchType.In);
        log.DeviceId.Should().Be("esp32-1");
        log.RecordedByUserId.Should().BeNull();
        log.DomainEvents.Should().ContainSingle(e => e is AttendanceLogRecorded);
    }

    [Fact]
    public void FromDevice_requires_employee_id()
    {
        var act = () => AttendanceLog.FromDevice(Guid.Empty, Now, PunchType.In, "esp32-1");

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance.employee_id");
    }

    [Fact]
    public void FromDevice_requires_device_id()
    {
        var act = () => AttendanceLog.FromDevice(Guid.NewGuid(), Now, PunchType.In, "  ");

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance.device_id");
    }

    [Fact]
    public void Manual_records_recorder_and_note()
    {
        var employeeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var log = AttendanceLog.Manual(employeeId, Now, PunchType.Out, userId, " lupa absen ");

        log.Source.Should().Be(AttendanceSource.Manual);
        log.RecordedByUserId.Should().Be(userId);
        log.Note.Should().Be("lupa absen");
        log.DeviceId.Should().BeNull();
        log.DomainEvents.OfType<AttendanceLogRecorded>().Should().HaveCount(1);
    }

    [Fact]
    public void Manual_requires_recorder()
    {
        var act = () => AttendanceLog.Manual(
            Guid.NewGuid(),
            Now,
            PunchType.In,
            Guid.Empty);

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance.recorded_by_required");
    }
}
