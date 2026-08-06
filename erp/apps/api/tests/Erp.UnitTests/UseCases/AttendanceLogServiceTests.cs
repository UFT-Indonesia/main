using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Common;
using FluentAssertions;
using NodaTime;
using NSubstitute;
using Wolverine;

namespace Erp.UnitTests.UseCases;

public class AttendanceLogServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 5, 12, 9, 0, 0, TimeSpan.FromHours(7));
    private static readonly EmployeeId ValidEmployeeId = EmployeeId.New();

    private static readonly EmployeeId TerminatedEmployeeId = EmployeeId.New();

    /// <summary>Manual entries carry a caller; device punches deliberately do not.</summary>
    private static readonly Caller OwnerCaller =
        new(Guid.NewGuid(), EmployeeRole.Owner, EmployeeId.New(), "Owner");

    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();
    private readonly IRepository<AttendanceLog> _attendanceLogs = Substitute.For<IRepository<AttendanceLog>>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    public AttendanceLogServiceTests()
    {
        var employee = Employee.Create(
            "Test Employee",
            Nik.Create("3201234567890123"),
            Money.Idr(5_000_000m),
            LocalDate.FromDateTime(DateTime.Today),
            EmployeeRole.Owner);

        _employees.GetByIdAsync(Arg.Any<EmployeeId>(), Arg.Any<CancellationToken>())
            .Returns((Employee?)null);
        _employees.GetByIdAsync(ValidEmployeeId, Arg.Any<CancellationToken>())
            .Returns(employee);

        var terminated = Employee.Create(
            "Gone Employee",
            Nik.Create("3201234567890124"),
            Money.Idr(5_000_000m),
            LocalDate.FromDateTime(DateTime.Today),
            EmployeeRole.Owner);
        terminated.Terminate(LocalDate.FromDateTime(DateTime.Today));
        _employees.GetByIdAsync(TerminatedEmployeeId, Arg.Any<CancellationToken>())
            .Returns(terminated);

        _clock.GetCurrentInstant().Returns(Instant.FromDateTimeOffset(Now));
    }

    [Fact]
    public async Task RecordAsync_rejects_a_device_punch_for_a_terminated_employee()
    {
        var result = await AttendanceLogService.RecordAsync(
            TerminatedEmployeeId.Value,
            Now,
            "In",
            recordedByUserId: null,
            recordedByName: null,
            deviceId: "esp32-1",
            note: null,
            null,
            _employees,
            _attendanceLogs,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceResult>.Error>()
            .Which.Code.Should().Be("attendance.employee_terminated");
        await _attendanceLogs.DidNotReceive().AddAsync(Arg.Any<AttendanceLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_rejects_a_manual_punch_for_a_terminated_employee()
    {
        var result = await AttendanceLogService.RecordAsync(
            TerminatedEmployeeId.Value,
            Now,
            "In",
            recordedByUserId: Guid.NewGuid(),
            recordedByName: "Manager",
            deviceId: null,
            note: "lupa absen",
            null,
            _employees,
            _attendanceLogs,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceResult>.Error>()
            .Which.Code.Should().Be("attendance.employee_terminated");
        await _attendanceLogs.DidNotReceive().AddAsync(Arg.Any<AttendanceLog>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("In")]
    [InlineData("Out")]
    [InlineData("in")]
    [InlineData("out")]
    [InlineData("IN")]
    [InlineData("OUT")]
    public async Task RecordAsync_device_log_succeeds_with_valid_punch_type(string punchType)
    {
        var result = await AttendanceLogService.RecordAsync(
            ValidEmployeeId.Value,
            Now,
            punchType,
            recordedByUserId: null,
            recordedByName: null,
            deviceId: "esp32-1",
            note: null,
            null,
            _employees,
            _attendanceLogs,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceResult>.Success>();
        await _attendanceLogs.Received(1).AddAsync(Arg.Any<AttendanceLog>(), Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(Arg.Any<AttendanceLogRecorded>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("invalid")]
    [InlineData("1")]
    [InlineData("0")]
    public async Task RecordAsync_returns_error_for_invalid_punch_type(string punchType)
    {
        var result = await AttendanceLogService.RecordAsync(
            ValidEmployeeId.Value,
            Now,
            punchType,
            recordedByUserId: null,
            recordedByName: null,
            deviceId: "esp32-1",
            note: null,
            null,
            _employees,
            _attendanceLogs,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceResult>.Error>()
            .Which.Code.Should().Be("attendance.punch_type");
        await _attendanceLogs.DidNotReceive().AddAsync(Arg.Any<AttendanceLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_returns_not_found_for_missing_employee()
    {
        var result = await AttendanceLogService.RecordAsync(
            Guid.NewGuid(),
            Now,
            "In",
            recordedByUserId: null,
            recordedByName: null,
            deviceId: "esp32-1",
            note: null,
            null,
            _employees,
            _attendanceLogs,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceResult>.NotFound>();
        await _attendanceLogs.DidNotReceive().AddAsync(Arg.Any<AttendanceLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_device_log_returns_error_for_empty_device_id()
    {
        var result = await AttendanceLogService.RecordAsync(
            ValidEmployeeId.Value,
            Now,
            "In",
            recordedByUserId: null,
            recordedByName: null,
            deviceId: "  ",
            note: null,
            null,
            _employees,
            _attendanceLogs,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceResult>.Error>()
            .Which.Code.Should().Be("attendance.device_id");
    }

    [Fact]
    public async Task RecordAsync_manual_log_succeeds()
    {
        var userId = Guid.NewGuid();

        var result = await AttendanceLogService.RecordAsync(
            ValidEmployeeId.Value,
            Now,
            "Out",
            recordedByUserId: userId,
            recordedByName: "Test Recorder",
            deviceId: null,
            note: "Forgot to punch",
            null,
            _employees,
            _attendanceLogs,
            _clock,
            _bus,
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<AttendanceResult>.Success>().Subject;
        success.Value.Source.Should().Be("Manual");
        success.Value.RecordedByUserId.Should().Be(userId);
        var note = success.Value.Notes.Should().ContainSingle().Subject;
        note.Text.Should().Be("Forgot to punch");
        note.CreatedByUserId.Should().Be(userId);
        note.CreatedByName.Should().Be("Test Recorder");
        note.CreatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task RecordAsync_manual_log_returns_error_for_empty_recorder()
    {
        var result = await AttendanceLogService.RecordAsync(
            ValidEmployeeId.Value,
            Now,
            "In",
            recordedByUserId: Guid.Empty,
            recordedByName: null,
            deviceId: null,
            note: null,
            null,
            _employees,
            _attendanceLogs,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceResult>.Error>()
            .Which.Code.Should().Be("attendance.recorded_by_required");
    }

    [Fact]
    public async Task RecordAsync_validates_punch_type_before_employee_lookup()
    {
        var result = await AttendanceLogService.RecordAsync(
            Guid.NewGuid(),
            Now,
            "invalid",
            recordedByUserId: null,
            recordedByName: null,
            deviceId: "esp32-1",
            note: null,
            null,
            _employees,
            _attendanceLogs,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceResult>.Error>()
            .Which.Code.Should().Be("attendance.punch_type");
        await _employees.DidNotReceive().GetByIdAsync(Arg.Any<EmployeeId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_does_not_call_SaveChangesAsync()
    {
        await AttendanceLogService.RecordAsync(
            ValidEmployeeId.Value,
            Now,
            "In",
            recordedByUserId: null,
            recordedByName: null,
            deviceId: "esp32-1",
            note: null,
            null,
            _employees,
            _attendanceLogs,
            _clock,
            _bus,
            CancellationToken.None);

        await _attendanceLogs.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
