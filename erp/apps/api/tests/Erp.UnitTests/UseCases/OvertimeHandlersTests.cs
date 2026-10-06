using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.CreateLeaveRequest;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Overtime.Common;
using Erp.UseCases.Overtime.Corrections;
using Erp.UseCases.Overtime.CreateOvertimeAssignment;
using Erp.UseCases.Overtime.GajiPremi;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NodaTime;
using NSubstitute;
using Wolverine;

namespace Erp.UnitTests.UseCases;

public class OvertimeHandlersTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 2, 3, 0); // Mon 2 Nov, 10:00 Jakarta
    private static readonly LocalDate Tuesday = new(2026, 10, 6);

    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();
    private readonly IRepository<OvertimeAssignment> _assignments = Substitute.For<IRepository<OvertimeAssignment>>();
    private readonly IReadRepository<OvertimeAssignment> _assignmentsRead = TestOvertime.None();
    private readonly IReadRepository<GajiPremiPeriod> _periods = Substitute.For<IReadRepository<GajiPremiPeriod>>();
    private readonly IReadRepository<AttendanceLog> _logs = Substitute.For<IReadRepository<AttendanceLog>>();
    private readonly IRepository<AttendanceDay> _days = Substitute.For<IRepository<AttendanceDay>>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IOptions<OvertimeOptions> _options = Options.Create(new OvertimeOptions
    {
        Tiers = [new(2, 25_000), new(4, 50_000), new(6, 70_000)],
    });

    private readonly Employee _owner = NewEmployee("Owner", EmployeeRole.Owner, null, "3201234567890123");
    private readonly Employee _manager;
    private readonly Employee _otherManager;
    private readonly Employee _staff;
    private readonly Employee _foreignStaff;

    public OvertimeHandlersTests()
    {
        _manager = NewEmployee("Manager", EmployeeRole.Manager, _owner.Id, "3201234567890124");
        _otherManager = NewEmployee("Other Manager", EmployeeRole.Manager, _owner.Id, "3201234567890125");
        _staff = NewEmployee("Staff", EmployeeRole.Staff, _manager.Id, "3201234567890126");
        _foreignStaff = NewEmployee("Foreign Staff", EmployeeRole.Staff, _otherManager.Id, "3201234567890127");

        _clock.GetCurrentInstant().Returns(Now);
        foreach (var e in new[] { _owner, _manager, _otherManager, _staff, _foreignStaff })
        {
            _employees.GetByIdAsync(e.Id, Arg.Any<CancellationToken>()).Returns(e);
        }

        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(new List<AttendanceLog>());
    }

    private static Employee NewEmployee(string name, EmployeeRole role, EmployeeId? parentId, string nik) =>
        Employee.Create(name, Nik.Create(nik), Money.Idr(5_000_000m), new LocalDate(2026, 1, 1), role, parentId);

    private static Caller CallerOf(Employee e) => new(Guid.NewGuid(), e.Role, e.Id, e.FullName);

    private Task<Result<OvertimeAssignmentResult>> CreateAsync(Employee subject, Employee by, DateOnly? date = null) =>
        CreateOvertimeAssignmentHandler.Handle(
            new CreateOvertimeAssignmentCommand(subject.Id.Value, date ?? Tuesday.ToDateOnly(), null, new TimeOnly(21, 30), CallerOf(by)),
            _employees, _assignments, _assignmentsRead, _periods, _logs, _days, TestPolicies.Standard, _options, _clock, CancellationToken.None);

    [Fact]
    public async Task An_owner_assigns_overtime_to_anyone_and_it_is_approved_at_once()
    {
        var result = await CreateAsync(_manager, _owner);

        var item = result.Should().BeOfType<Result<OvertimeAssignmentResult>.Success>().Subject.Value;
        item.Status.Should().Be("Approved");
        item.IsDayOff.Should().BeFalse();
        await _assignments.Received(1).AddAsync(Arg.Any<OvertimeAssignment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_manager_assigns_to_their_own_staff_and_it_waits_for_an_owner()
    {
        var result = await CreateAsync(_staff, _manager);

        result.Should().BeOfType<Result<OvertimeAssignmentResult>.Success>().Which.Value.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task A_day_off_is_snapshotted_from_the_holiday_calendar()
    {
        var holiday = Tuesday.PlusDays(1);
        var policy = TestPolicies.Standard with { Holidays = new HashSet<LocalDate> { holiday } };

        var result = await CreateOvertimeAssignmentHandler.Handle(
            new CreateOvertimeAssignmentCommand(_staff.Id.Value, holiday.ToDateOnly(), new TimeOnly(9, 0), new TimeOnly(16, 0), CallerOf(_owner)),
            _employees, _assignments, _assignmentsRead, _periods, _logs, _days, policy, _options, _clock, CancellationToken.None);

        result.Should().BeOfType<Result<OvertimeAssignmentResult>.Success>().Which.Value.IsDayOff.Should().BeTrue();
    }

    [Fact]
    public async Task A_manager_cannot_assign_to_another_managers_staff_or_to_an_owner()
    {
        (await CreateAsync(_foreignStaff, _manager)).Should().BeOfType<Result<OvertimeAssignmentResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
        (await CreateAsync(_owner, _owner)).Should().BeOfType<Result<OvertimeAssignmentResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    [Fact]
    public async Task Staff_cannot_assign_overtime_at_all() =>
        (await CreateAsync(_staff, _staff)).Should().BeOfType<Result<OvertimeAssignmentResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);

    [Fact]
    public async Task One_overtime_per_employee_per_date()
    {
        _assignmentsRead.AnyAsync(Arg.Any<ISpecification<OvertimeAssignment>>(), Arg.Any<CancellationToken>()).Returns(true);

        (await CreateAsync(_staff, _owner)).Should().BeOfType<Result<OvertimeAssignmentResult>.Error>()
            .Which.Code.Should().Be("overtime.duplicate_date");
    }

    [Fact]
    public async Task A_closed_period_refuses_new_overtime()
    {
        _periods.AnyAsync(Arg.Any<ISpecification<GajiPremiPeriod>>(), Arg.Any<CancellationToken>()).Returns(true);

        (await CreateAsync(_staff, _owner)).Should().BeOfType<Result<OvertimeAssignmentResult>.Error>()
            .Which.Code.Should().Be("overtime.period_closed");
    }

    [Fact]
    public async Task Leave_is_refused_on_a_date_with_overtime()
    {
        var leaveRequests = Substitute.For<IRepository<LeaveRequest>>();
        leaveRequests.ListAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>()).Returns(new List<LeaveRequest>());
        var overtime = Substitute.For<IReadRepository<OvertimeAssignment>>();
        overtime.AnyAsync(Arg.Any<ISpecification<OvertimeAssignment>>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateLeaveRequestHandler.Handle(
            new CreateLeaveRequestCommand(
                _staff.Id.Value, "Annual", Tuesday.ToDateOnly(), Tuesday.ToDateOnly(), "cuti", null, false, null, null, null, CallerOf(_staff)),
            _employees, leaveRequests, overtime, TestPolicies.Standard, _clock, Substitute.For<IMessageBus>(), CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>().Which.Code.Should().Be("leave.overtime_on_date");
    }

    [Fact]
    public async Task Closing_freezes_the_counted_pay_and_expires_what_nobody_decided()
    {
        var approved = OvertimeAssignment.Create(
            _staff.Id, Tuesday, null, new LocalTime(21, 30), false, Guid.NewGuid(), "Owner", Now, autoApprove: true);
        var undecided = OvertimeAssignment.Create(
            _manager.Id, Tuesday, null, new LocalTime(21, 30), false, Guid.NewGuid(), "Owner", Now, autoApprove: false);
        var punches = new List<AttendanceLog>
        {
            AttendanceLog.Manual(_staff.Id, Instant.FromUtc(2026, 10, 6, 11, 31), PunchType.In, Guid.NewGuid()), // 18:31
            AttendanceLog.Manual(_staff.Id, Instant.FromUtc(2026, 10, 6, 14, 35), PunchType.Out, Guid.NewGuid()), // 21:35
        };

        var tracked = Substitute.For<IRepository<OvertimeAssignment>>();
        tracked.ListAsync(Arg.Any<ISpecification<OvertimeAssignment>>(), Arg.Any<CancellationToken>())
            .Returns(new List<OvertimeAssignment> { approved, undecided });
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(punches);
        var corrections = Substitute.For<IRepository<OvertimeCorrectionRequest>>();
        corrections.ListAsync(Arg.Any<ISpecification<OvertimeCorrectionRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new List<OvertimeCorrectionRequest>());
        var periods = Substitute.For<IRepository<GajiPremiPeriod>>();
        var rapels = Substitute.For<IReadRepository<Rapel>>();
        rapels.ListAsync(Arg.Any<ISpecification<Rapel>>(), Arg.Any<CancellationToken>()).Returns(new List<Rapel>());
        var policy = TestPolicies.Standard with { ClockInGraceMinutes = 5, ClockOutGraceMinutes = 5 };

        var result = await CloseGajiPremiPeriodHandler.Handle(
            new CloseGajiPremiPeriodCommand(new DateOnly(2026, 9, 1), CallerOf(_owner)),
            tracked, _assignmentsRead, corrections, periods, _periods, rapels,
            _logs, policy, _options, _clock, CancellationToken.None);

        result.Should().BeOfType<Result<GajiPremiPeriodResult>.Success>();
        approved.FrozenHours.Should().Be(3);
        approved.FrozenAmount.Should().Be(25_000);
        undecided.Status.Should().Be(OvertimeStatus.Expired);
        undecided.FrozenAmount.Should().Be(0);
        await periods.Received(1).AddAsync(
            Arg.Is<GajiPremiPeriod>(p => p.StartDate == new LocalDate(2026, 9, 1)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_period_cannot_be_closed_before_it_ends_or_by_a_non_owner()
    {
        Task<Result<GajiPremiPeriodResult>> Close(DateOnly start, Employee by) => CloseGajiPremiPeriodHandler.Handle(
            new CloseGajiPremiPeriodCommand(start, CallerOf(by)),
            _assignments, _assignmentsRead, Substitute.For<IRepository<OvertimeCorrectionRequest>>(),
            Substitute.For<IRepository<GajiPremiPeriod>>(), _periods, Substitute.For<IReadRepository<Rapel>>(),
            _logs, TestPolicies.Standard, _options, _clock, CancellationToken.None);

        (await Close(new DateOnly(2026, 11, 1), _owner)).Should().BeOfType<Result<GajiPremiPeriodResult>.Error>()
            .Which.Code.Should().Be("gaji_premi.not_ended");
        (await Close(new DateOnly(2026, 9, 1), _manager)).Should().BeOfType<Result<GajiPremiPeriodResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
        (await Close(new DateOnly(2026, 10, 1), _owner)).Should().BeOfType<Result<GajiPremiPeriodResult>.Error>()
            .Which.Code.Should().Be("gaji_premi.period");
    }

    private (OvertimeCorrectionRequest Request, OvertimeAssignment Assignment) FiledTapOutCorrection()
    {
        var assignment = OvertimeAssignment.Create(
            _staff.Id, Tuesday, null, new LocalTime(21, 30), false, Guid.NewGuid(), "Owner", Now, autoApprove: true);
        var request = OvertimeCorrectionRequest.Create(
            assignment.Id, _staff.Id, Tuesday, OvertimeCorrectionKind.TapOut, Instant.FromUtc(2026, 10, 6, 14, 30), "lupa tap out",
            TestAttachments.DoctorsNote(), CallerOf(_staff).UserId, Now);
        typeof(OvertimeCorrectionRequest).GetProperty(nameof(OvertimeCorrectionRequest.Employee))!.SetValue(request, _staff);
        typeof(OvertimeAssignment).GetProperty(nameof(OvertimeAssignment.Employee))!.SetValue(assignment, _staff);

        _assignmentsRead.FirstOrDefaultAsync(Arg.Any<ISpecification<OvertimeAssignment>>(), Arg.Any<CancellationToken>()).Returns(assignment);
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(new List<AttendanceLog>
        {
            AttendanceLog.Manual(_staff.Id, Instant.FromUtc(2026, 10, 6, 11, 31), PunchType.In, Guid.NewGuid()), // 18:31, no tap-out yet
        });
        return (request, assignment);
    }

    private Task<Result<OvertimeCorrectionResult>> DecideCorrectionAsync(
        OvertimeCorrectionRequest request, Employee by, bool approve, IRepository<AttendanceLog> logWriter)
    {
        var corrections = Substitute.For<IRepository<OvertimeCorrectionRequest>>();
        corrections.FirstOrDefaultAsync(Arg.Any<ISpecification<OvertimeCorrectionRequest>>(), Arg.Any<CancellationToken>()).Returns(request);
        return DecideOvertimeCorrectionHandler.Handle(
            new DecideOvertimeCorrectionCommand(Guid.NewGuid(), approve, null, CallerOf(by)),
            corrections, _assignmentsRead, _employees, _logs, logWriter, _days, TestPolicies.Standard, _clock,
            Substitute.For<IMessageBus>(), CancellationToken.None);
    }

    [Fact]
    public async Task Approving_a_correction_writes_the_missing_punch_recorded_by_the_decider()
    {
        var (request, _) = FiledTapOutCorrection();
        var logWriter = Substitute.For<IRepository<AttendanceLog>>();

        var result = await DecideCorrectionAsync(request, _manager, approve: true, logWriter);

        result.Should().BeOfType<Result<OvertimeCorrectionResult>.Success>();
        request.Status.Should().Be(OvertimeRequestStatus.Approved);
        await logWriter.Received(1).AddAsync(
            Arg.Is<AttendanceLog>(l => l.PunchType == PunchType.Out && l.EmployeeId == _staff.Id
                && l.PunchedAtUtc == Instant.FromUtc(2026, 10, 6, 14, 30) && l.RecordedByUserId != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_correction_is_decided_by_the_manager_or_an_owner_never_the_employee_or_another_manager()
    {
        var (request, _) = FiledTapOutCorrection();
        var logWriter = Substitute.For<IRepository<AttendanceLog>>();

        foreach (var outsider in new[] { _staff, _otherManager })
        {
            (await DecideCorrectionAsync(request, outsider, approve: true, logWriter))
                .Should().BeOfType<Result<OvertimeCorrectionResult>.Error>().Which.Code.Should().Be(ResultErrors.Forbidden);
        }

        request.Status.Should().Be(OvertimeRequestStatus.Pending);
        await logWriter.DidNotReceive().AddAsync(Arg.Any<AttendanceLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_tap_out_correction_is_allowed_after_an_early_tap_out_and_the_new_punch_becomes_the_tap_out()
    {
        var (request, _) = FiledTapOutCorrection();
        var logWriter = Substitute.For<IRepository<AttendanceLog>>();
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(new List<AttendanceLog>
        {
            AttendanceLog.Manual(_staff.Id, Instant.FromUtc(2026, 10, 6, 11, 31), PunchType.In, Guid.NewGuid()),
            AttendanceLog.Manual(_staff.Id, Instant.FromUtc(2026, 10, 6, 13, 15), PunchType.Out, Guid.NewGuid()), // 20:15, by mistake
        });

        (await DecideCorrectionAsync(request, _manager, approve: true, logWriter))
            .Should().BeOfType<Result<OvertimeCorrectionResult>.Success>();
        await logWriter.Received(1).AddAsync(
            Arg.Is<AttendanceLog>(l => l.PunchType == PunchType.Out && l.PunchedAtUtc == Instant.FromUtc(2026, 10, 6, 14, 30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_correction_is_refused_when_the_punch_is_not_actually_missing_or_outside_the_window()
    {
        var (request, _) = FiledTapOutCorrection();
        var logWriter = Substitute.For<IRepository<AttendanceLog>>();
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(new List<AttendanceLog>
        {
            AttendanceLog.Manual(_staff.Id, Instant.FromUtc(2026, 10, 6, 11, 31), PunchType.In, Guid.NewGuid()),
            AttendanceLog.Manual(_staff.Id, Instant.FromUtc(2026, 10, 6, 14, 28), PunchType.Out, Guid.NewGuid()),
        });

        (await DecideCorrectionAsync(request, _manager, approve: true, logWriter))
            .Should().BeOfType<Result<OvertimeCorrectionResult>.Error>().Which.Code.Should().Be("overtime_correction.not_missing");
    }
}
