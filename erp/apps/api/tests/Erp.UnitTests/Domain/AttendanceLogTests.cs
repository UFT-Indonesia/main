using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.Domain;

public class AttendanceLogTests
{
    private static readonly Instant Now = Instant.FromUtc(2025, 5, 12, 2, 0); // 09:00 WIB

    [Fact]
    public void FromDevice_records_log_and_event()
    {
        var employeeId = EmployeeId.New();

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
        var act = () => AttendanceLog.FromDevice(EmployeeId.Empty, Now, PunchType.In, "esp32-1");

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance.employee_id");
    }

    [Fact]
    public void FromDevice_requires_device_id()
    {
        var act = () => AttendanceLog.FromDevice(EmployeeId.New(), Now, PunchType.In, "  ");

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance.device_id");
    }

    [Fact]
    public void Manual_records_recorder()
    {
        var employeeId = EmployeeId.New();
        var userId = Guid.NewGuid();

        var log = AttendanceLog.Manual(employeeId, Now, PunchType.Out, userId);

        log.Source.Should().Be(AttendanceSource.Manual);
        log.RecordedByUserId.Should().Be(userId);
        log.Notes.Should().BeEmpty();
        log.DeviceId.Should().BeNull();
        log.DomainEvents.OfType<AttendanceLogRecorded>().Should().HaveCount(1);
    }

    [Fact]
    public void AddNote_appends_authored_note_and_trims_text()
    {
        var log = AttendanceLog.Manual(EmployeeId.New(), Now, PunchType.Out, Guid.NewGuid());
        var author = Guid.NewGuid();

        var note = log.AddNote(" lupa absen ", author, " Budi ", Now);

        note.Text.Should().Be("lupa absen");
        note.CreatedByUserId.Should().Be(author);
        note.CreatedByName.Should().Be("Budi");
        note.CreatedAtUtc.Should().Be(Now);
        note.AttendanceLogId.Should().Be(log.Id);
        log.Notes.Should().ContainSingle().Which.Should().BeSameAs(note);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddNote_requires_text(string text)
    {
        var log = AttendanceLog.Manual(EmployeeId.New(), Now, PunchType.Out, Guid.NewGuid());

        var act = () => log.AddNote(text, Guid.NewGuid(), "Budi", Now);

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance_note.text");
    }

    [Fact]
    public void AddNote_rejects_over_length_text()
    {
        var log = AttendanceLog.Manual(EmployeeId.New(), Now, PunchType.Out, Guid.NewGuid());

        var act = () => log.AddNote(new string('x', AttendanceLog.NoteMaxLength + 1), Guid.NewGuid(), "Budi", Now);

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance_note.text_length");
    }

    [Fact]
    public void AddNote_requires_author()
    {
        var log = AttendanceLog.Manual(EmployeeId.New(), Now, PunchType.Out, Guid.NewGuid());

        var act = () => log.AddNote("catatan", Guid.Empty, "Budi", Now);

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance_note.author");
    }

    [Fact]
    public void RemoveNote_removes_own_note_and_rejects_unknown_id()
    {
        var log = AttendanceLog.Manual(EmployeeId.New(), Now, PunchType.Out, Guid.NewGuid());
        var note = log.AddNote("catatan", Guid.NewGuid(), "Budi", Now);

        log.RemoveNote(Guid.NewGuid()).Should().BeFalse();
        log.RemoveNote(note.Id).Should().BeTrue();
        log.Notes.Should().BeEmpty();
    }

    [Fact]
    public void Manual_requires_recorder()
    {
        var act = () => AttendanceLog.Manual(
            EmployeeId.New(),
            Now,
            PunchType.In,
            Guid.Empty);

        act.Should().Throw<DomainException>().Where(e => e.Code == "attendance.recorded_by_required");
    }
}
