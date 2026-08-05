using Ardalis.Specification;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Leave.Common;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class EmployeeTerminatedLeaveHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 8, 5, 9, 0);

    private readonly IRepository<LeaveRequest> _leaveRequests = Substitute.For<IRepository<LeaveRequest>>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly EmployeeId _employeeId = EmployeeId.New();

    public EmployeeTerminatedLeaveHandlerTests()
    {
        _clock.GetCurrentInstant().Returns(Now);
    }

    private LeaveRequest PendingRequest() => LeaveRequest.Create(
        _employeeId,
        LeaveType.Annual,
        new LocalDate(2026, 9, 1),
        new LocalDate(2026, 9, 4),
        "cuti",
        Guid.NewGuid(),
        Now);

    private Task InvokeAsync() => EmployeeTerminatedLeaveHandler.Handle(
        new EmployeeTerminated(_employeeId.Value, new LocalDate(2026, 8, 5)),
        _leaveRequests,
        _clock,
        CancellationToken.None);

    [Fact]
    public async Task Cancels_every_pending_request_for_the_terminated_employee()
    {
        var first = PendingRequest();
        var second = PendingRequest();
        _leaveRequests.ListAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns([first, second]);

        await InvokeAsync();

        first.Status.Should().Be(LeaveRequestStatus.Cancelled);
        second.Status.Should().Be(LeaveRequestStatus.Cancelled);
        first.DecidedByName.Should().Be(LeaveRequest.SystemDecider);
        first.DecidedAtUtc.Should().Be(Now);
        await _leaveRequests.Received(2).UpdateAsync(Arg.Any<LeaveRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_nothing_when_there_is_nothing_pending()
    {
        _leaveRequests.ListAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await InvokeAsync();

        await _leaveRequests.DidNotReceive().UpdateAsync(Arg.Any<LeaveRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Approved_leave_survives_termination_as_the_historical_record()
    {
        var request = PendingRequest();
        request.Approve(Guid.NewGuid(), "Owner", Now);

        request.CancelForTermination(Now);

        request.Status.Should().Be(LeaveRequestStatus.Approved);
        request.DecidedByName.Should().Be("Owner");
    }
}
