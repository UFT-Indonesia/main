using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.AddAttendanceLogNote;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Attendance.DeleteAttendanceLogNote;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class AttendanceLogNoteHandlersTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 14, 8, 0);

    private readonly IRepository<AttendanceLog> _attendanceLogs = Substitute.For<IRepository<AttendanceLog>>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly AttendanceLog _log = AttendanceLog.Manual(
        EmployeeId.New(), Instant.FromUtc(2026, 7, 14, 1, 0), PunchType.In, Guid.NewGuid());

    public AttendanceLogNoteHandlersTests()
    {
        _clock.GetCurrentInstant().Returns(Now);
        _attendanceLogs.FirstOrDefaultAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>())
            .Returns(_log);
    }

    [Fact]
    public async Task AddNote_appends_and_persists()
    {
        var author = Guid.NewGuid();

        var result = await AddAttendanceLogNoteHandler.Handle(
            new AddAttendanceLogNoteCommand(_log.Id.Value, "izin telat", author, "Budi"),
            _attendanceLogs,
            _clock,
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<AttendanceLogNoteResult>.Success>().Subject;
        success.Value.Text.Should().Be("izin telat");
        success.Value.CreatedByName.Should().Be("Budi");
        success.Value.CreatedAtUtc.Should().Be(Now.ToDateTimeOffset());
        _log.Notes.Should().ContainSingle();
        await _attendanceLogs.Received(1).UpdateAsync(_log, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddNote_returns_not_found_for_missing_log()
    {
        _attendanceLogs.FirstOrDefaultAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>())
            .Returns((AttendanceLog?)null);

        var result = await AddAttendanceLogNoteHandler.Handle(
            new AddAttendanceLogNoteCommand(Guid.NewGuid(), "izin", Guid.NewGuid(), "Budi"),
            _attendanceLogs,
            _clock,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceLogNoteResult>.NotFound>();
    }

    [Fact]
    public async Task DeleteNote_removes_and_persists()
    {
        var note = _log.AddNote("salah tulis", Guid.NewGuid(), "Budi", Now);

        var result = await DeleteAttendanceLogNoteHandler.Handle(
            new DeleteAttendanceLogNoteCommand(_log.Id.Value, note.Id),
            _attendanceLogs,
            CancellationToken.None);

        result.Should().BeOfType<Result<bool>.Success>();
        _log.Notes.Should().BeEmpty();
        await _attendanceLogs.Received(1).UpdateAsync(_log, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteNote_returns_not_found_for_unknown_note_and_does_not_persist()
    {
        var result = await DeleteAttendanceLogNoteHandler.Handle(
            new DeleteAttendanceLogNoteCommand(_log.Id.Value, Guid.NewGuid()),
            _attendanceLogs,
            CancellationToken.None);

        result.Should().BeOfType<Result<bool>.NotFound>();
        await _attendanceLogs.DidNotReceive().UpdateAsync(Arg.Any<AttendanceLog>(), Arg.Any<CancellationToken>());
    }
}
