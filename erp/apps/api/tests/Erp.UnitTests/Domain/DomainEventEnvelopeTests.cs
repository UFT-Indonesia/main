using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.Domain;

public class DomainEventEnvelopeTests
{
    private static readonly Nik SampleNik = Nik.Create("3201234567890123");
    private static readonly Money Wage = Money.Idr(8_000_000m);
    private static readonly LocalDate EffectiveFrom = new(2025, 1, 1);

    [Fact]
    public void Every_event_carries_unique_event_id_and_raised_at()
    {
        var owner = Employee.Create("Owner", SampleNik, Wage, EffectiveFrom, EmployeeRole.Owner);
        var ev = owner.DomainEvents.OfType<EmployeeCreated>().Single();

        ev.EventId.Should().NotBe(Guid.Empty);
        ev.RaisedAt.Should().BeGreaterThan(Instant.FromUtc(2024, 1, 1, 0, 0));
        ev.EventVersion.Should().Be(1);
    }

    [Fact]
    public void EmployeeCreated_envelope_metadata_is_set()
    {
        var owner = Employee.Create("Owner", SampleNik, Wage, EffectiveFrom, EmployeeRole.Owner);
        var ev = owner.DomainEvents.OfType<EmployeeCreated>().Single();

        ev.EventType.Should().Be("employee.created");
        ev.AggregateType.Should().Be(nameof(Employee));
        ev.AggregateId.Should().Be(owner.Id);
    }

    [Fact]
    public void EmployeeCreated_payload_contains_full_initial_snapshot()
    {
        var parentId = Guid.NewGuid();
        var manager = Employee.Create(
            "Manager Satu",
            Nik.Create("3201234567890124"),
            Wage,
            EffectiveFrom,
            EmployeeRole.Manager,
            parentId: parentId,
            npwp: Npwp.Create("123456789012000"));

        var ev = manager.DomainEvents.OfType<EmployeeCreated>().Single();

        ev.FullName.Should().Be("Manager Satu");
        ev.Nik.Should().Be("3201234567890124");
        ev.Npwp.Should().Be("123456789012000");
        ev.Role.Should().Be(EmployeeRole.Manager);
        ev.ParentId.Should().Be(parentId);
        ev.MonthlyWage.Should().Be(Wage);
        ev.EffectiveSalaryFrom.Should().Be(EffectiveFrom);
    }

    [Fact]
    public void EmployeeSalaryChanged_carries_old_and_new()
    {
        var owner = Employee.Create("Owner", SampleNik, Wage, EffectiveFrom, EmployeeRole.Owner);
        var newWage = Money.Idr(10_000_000m);
        var newEffective = EffectiveFrom.PlusMonths(6);

        owner.ClearDomainEvents();
        owner.ChangeSalary(newWage, newEffective);
        var ev = owner.DomainEvents.OfType<EmployeeSalaryChanged>().Single();

        ev.OldMonthlyWage.Should().Be(Wage);
        ev.OldEffectiveFrom.Should().Be(EffectiveFrom);
        ev.NewMonthlyWage.Should().Be(newWage);
        ev.NewEffectiveFrom.Should().Be(newEffective);
        ev.AggregateId.Should().Be(owner.Id);
        ev.EventType.Should().Be("employee.salary_changed");
    }

    [Fact]
    public void AttendanceLogRecorded_distinguishes_raised_at_from_punched_at()
    {
        // Backfilled punch: business fact is from 2 days ago, but the event
        // is raised now. Consumers must be able to tell them apart.
        var twoDaysAgoPunch = SystemClock.Instance.GetCurrentInstant() - Duration.FromDays(2);
        var log = AttendanceLog.Manual(
            Guid.NewGuid(),
            twoDaysAgoPunch,
            PunchType.In,
            Guid.NewGuid(),
            note: "lupa absen");

        var ev = log.DomainEvents.OfType<AttendanceLogRecorded>().Single();

        ev.PunchedAtUtc.Should().Be(twoDaysAgoPunch);
        ev.RaisedAt.Should().BeGreaterThan(twoDaysAgoPunch);
        (ev.RaisedAt - ev.PunchedAtUtc).Should().BeGreaterThan(Duration.FromHours(24));
    }

    [Fact]
    public void AttendanceLogRecorded_envelope_targets_log_aggregate()
    {
        var log = AttendanceLog.FromDevice(
            Guid.NewGuid(),
            SystemClock.Instance.GetCurrentInstant(),
            PunchType.In,
            "esp32-1");

        var ev = log.DomainEvents.OfType<AttendanceLogRecorded>().Single();

        ev.EventType.Should().Be("attendance.recorded");
        ev.AggregateType.Should().Be(nameof(AttendanceLog));
        ev.AggregateId.Should().Be(log.Id);
        ev.LogId.Should().Be(log.Id);
    }

    [Fact]
    public void AttendanceLogRecorded_payload_carries_device_context_for_device_source()
    {
        var log = AttendanceLog.FromDevice(
            Guid.NewGuid(),
            SystemClock.Instance.GetCurrentInstant(),
            PunchType.Out,
            "esp32-7");

        var ev = log.DomainEvents.OfType<AttendanceLogRecorded>().Single();

        ev.Source.Should().Be(AttendanceSource.Device);
        ev.DeviceId.Should().Be("esp32-7");
        ev.RecordedByUserId.Should().BeNull();
        ev.Note.Should().BeNull();
    }

    [Fact]
    public void AttendanceLogRecorded_payload_carries_recorder_context_for_manual_source()
    {
        var userId = Guid.NewGuid();
        var log = AttendanceLog.Manual(
            Guid.NewGuid(),
            SystemClock.Instance.GetCurrentInstant(),
            PunchType.In,
            userId,
            note: "koreksi manual");

        var ev = log.DomainEvents.OfType<AttendanceLogRecorded>().Single();

        ev.Source.Should().Be(AttendanceSource.Manual);
        ev.DeviceId.Should().BeNull();
        ev.RecordedByUserId.Should().Be(userId);
        ev.Note.Should().Be("koreksi manual");
    }
}
