using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.AddAttendanceLogNote;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Attendance.DeleteAttendanceLogNote;
using Erp.UseCases.Common;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class AttendanceLogNoteHandlersTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 14, 8, 0);

    private readonly IRepository<AttendanceLog> _attendanceLogs = Substitute.For<IRepository<AttendanceLog>>();
    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly Employee _subject = Employee.Create(
        "Subject Employee",
        Nik.Create("3201234567890123"),
        Money.Idr(5_000_000m),
        new LocalDate(2026, 1, 1),
        EmployeeRole.Owner);

    private readonly AttendanceLog _log;
    private readonly Caller _owner = new(Guid.NewGuid(), EmployeeRole.Owner, EmployeeId.New(), "Budi");

    public AttendanceLogNoteHandlersTests()
    {
        _log = AttendanceLog.Manual(
            _subject.Id, Instant.FromUtc(2026, 7, 14, 1, 0), PunchType.In, Guid.NewGuid());

        _clock.GetCurrentInstant().Returns(Now);
        _attendanceLogs.FirstOrDefaultAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>())
            .Returns(_log);
        _employees.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>()).Returns(_subject);
    }

    [Fact]
    public async Task AddNote_appends_and_persists()
    {
        var result = await AddAttendanceLogNoteHandler.Handle(
            new AddAttendanceLogNoteCommand(_log.Id.Value, "izin telat", _owner),
            _attendanceLogs,
            _employees,
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
            new AddAttendanceLogNoteCommand(Guid.NewGuid(), "izin", _owner),
            _attendanceLogs,
            _employees,
            _clock,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceLogNoteResult>.NotFound>();
    }

    [Fact]
    public async Task AddNote_is_refused_outside_the_callers_reporting_line()
    {
        // A Manager who does not own this employee cannot annotate their punches.
        var outsider = new Caller(Guid.NewGuid(), EmployeeRole.Manager, EmployeeId.New(), "Manager Lain");

        var result = await AddAttendanceLogNoteHandler.Handle(
            new AddAttendanceLogNoteCommand(_log.Id.Value, "izin telat", outsider),
            _attendanceLogs,
            _employees,
            _clock,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceLogNoteResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
        await _attendanceLogs.DidNotReceive().UpdateAsync(Arg.Any<AttendanceLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteNote_removes_and_persists()
    {
        var note = _log.AddNote("salah tulis", Guid.NewGuid(), "Budi", Now);

        var result = await DeleteAttendanceLogNoteHandler.Handle(
            new DeleteAttendanceLogNoteCommand(_log.Id.Value, note.Id, _owner),
            _attendanceLogs,
            _employees,
            CancellationToken.None);

        result.Should().BeOfType<Result<bool>.Success>();
        _log.Notes.Should().BeEmpty();
        await _attendanceLogs.Received(1).UpdateAsync(_log, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteNote_returns_not_found_for_unknown_note_and_does_not_persist()
    {
        var result = await DeleteAttendanceLogNoteHandler.Handle(
            new DeleteAttendanceLogNoteCommand(_log.Id.Value, Guid.NewGuid(), _owner),
            _attendanceLogs,
            _employees,
            CancellationToken.None);

        result.Should().BeOfType<Result<bool>.NotFound>();
        await _attendanceLogs.DidNotReceive().UpdateAsync(Arg.Any<AttendanceLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteNote_is_refused_outside_the_callers_reporting_line()
    {
        var note = _log.AddNote("salah tulis", Guid.NewGuid(), "Budi", Now);
        var outsider = new Caller(Guid.NewGuid(), EmployeeRole.Manager, EmployeeId.New(), "Manager Lain");

        var result = await DeleteAttendanceLogNoteHandler.Handle(
            new DeleteAttendanceLogNoteCommand(_log.Id.Value, note.Id, outsider),
            _attendanceLogs,
            _employees,
            CancellationToken.None);

        result.Should().BeOfType<Result<bool>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
        _log.Notes.Should().ContainSingle();
    }
}
