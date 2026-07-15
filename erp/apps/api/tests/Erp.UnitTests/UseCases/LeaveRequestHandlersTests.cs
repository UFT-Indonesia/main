using Ardalis.Specification;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.CreateLeaveRequest;
using Erp.UseCases.Leave.DecideLeaveRequest;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class LeaveRequestHandlersTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 14, 8, 0);

    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();
    private readonly IRepository<LeaveRequest> _leaveRequests = Substitute.For<IRepository<LeaveRequest>>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly Employee _employee = Employee.Create(
        "Test Employee",
        Nik.Create("3201234567890123"),
        Money.Idr(5_000_000m),
        LocalDate.FromDateTime(DateTime.Today),
        EmployeeRole.Owner);

    public LeaveRequestHandlersTests()
    {
        _clock.GetCurrentInstant().Returns(Now);
        _employees.GetByIdAsync(_employee.Id, Arg.Any<CancellationToken>()).Returns(_employee);
        _leaveRequests.AnyAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(false);
    }

    private CreateLeaveRequestCommand ValidCommand() => new(
        _employee.Id.Value,
        "Annual",
        new DateOnly(2026, 8, 3),
        new DateOnly(2026, 8, 7),
        "acara keluarga",
        Guid.NewGuid());

    [Fact]
    public async Task Create_succeeds_and_persists()
    {
        var result = await CreateLeaveRequestHandler.Handle(
            ValidCommand(), _employees, _leaveRequests, _clock, CancellationToken.None);

        var success = result.Should().BeOfType<Result<LeaveRequestResult>.Success>().Subject;
        success.Value.Status.Should().Be("Pending");
        success.Value.WorkdayCount.Should().Be(5);
        success.Value.EmployeeFullName.Should().Be("Test Employee");
        await _leaveRequests.Received(1).AddAsync(Arg.Any<LeaveRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_invalid_type()
    {
        var command = ValidCommand() with { Type = "Vacation" };

        var result = await CreateLeaveRequestHandler.Handle(
            command, _employees, _leaveRequests, _clock, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.type");
    }

    [Fact]
    public async Task Create_returns_not_found_for_unknown_employee()
    {
        var command = ValidCommand() with { EmployeeId = Guid.NewGuid() };

        var result = await CreateLeaveRequestHandler.Handle(
            command, _employees, _leaveRequests, _clock, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.NotFound>();
    }

    [Fact]
    public async Task Create_rejects_when_pending_request_exists()
    {
        _leaveRequests.AnyAsync(Arg.Any<PendingLeaveForEmployeeSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateLeaveRequestHandler.Handle(
            ValidCommand(), _employees, _leaveRequests, _clock, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.pending_exists");
        await _leaveRequests.DidNotReceive().AddAsync(Arg.Any<LeaveRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_overlap_with_approved_leave()
    {
        _leaveRequests.AnyAsync(Arg.Any<ApprovedLeaveOverlappingSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateLeaveRequestHandler.Handle(
            ValidCommand(), _employees, _leaveRequests, _clock, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.overlaps_approved");
    }

    [Fact]
    public async Task Approve_transitions_and_persists()
    {
        var request = LeaveRequest.Create(
            _employee.Id, LeaveType.Annual,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 7),
            null, Guid.NewGuid(), Now);
        _leaveRequests.FirstOrDefaultAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(request);

        var result = await ApproveLeaveRequestHandler.Handle(
            new ApproveLeaveRequestCommand(request.Id.Value, Guid.NewGuid(), "Budi"),
            _leaveRequests, _clock, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>()
            .Which.Value.Status.Should().Be("Approved");
        request.DecidedByName.Should().Be("Budi");
        await _leaveRequests.Received(1).UpdateAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Decide_returns_not_found_for_missing_request()
    {
        _leaveRequests.FirstOrDefaultAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns((LeaveRequest?)null);

        var result = await DenyLeaveRequestHandler.Handle(
            new DenyLeaveRequestCommand(Guid.NewGuid(), Guid.NewGuid(), "Budi", null),
            _leaveRequests, _clock, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.NotFound>();
    }

    [Fact]
    public async Task Cancel_records_note()
    {
        var request = LeaveRequest.Create(
            _employee.Id, LeaveType.Sick,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            null, Guid.NewGuid(), Now);
        request.Approve(Guid.NewGuid(), "Budi", Now);
        _leaveRequests.FirstOrDefaultAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(request);

        var result = await CancelLeaveRequestHandler.Handle(
            new CancelLeaveRequestCommand(request.Id.Value, Guid.NewGuid(), "Sari", "sembuh lebih cepat"),
            _leaveRequests, _clock, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>()
            .Which.Value.Status.Should().Be("Cancelled");
        request.DecisionNote.Should().Be("sembuh lebih cepat");
    }
}
