using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.GetBlockedLeaveDates;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class GetBlockedLeaveDatesHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 14, 8, 0);
    private static readonly EmployeeId Employee = EmployeeId.New();
    private readonly IReadRepository<LeaveRequest> _leaveRequests = Substitute.For<IReadRepository<LeaveRequest>>();
    private readonly Caller _caller = new(Guid.NewGuid(), EmployeeRole.Staff, Employee, "Budi");

    private void Approved(params LeaveRequest[] requests) =>
        _leaveRequests.ListAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(requests.ToList());

    [Fact]
    public async Task A_plain_approved_day_blocks_a_full_day_candidate()
    {
        var existing = ApprovedFullDay(new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3));
        Approved(existing);

        var result = await GetBlockedLeaveDatesHandler.Handle(
            new GetBlockedLeaveDatesQuery(
                Employee.Value, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 3),
                HalfDay: false, HalfDayPeriod: null, StartHour: null, EndHour: null, _caller),
            _leaveRequests, TestPolicies.Standard, CancellationToken.None);

        var value = result.Should().BeOfType<Result<BlockedLeaveDatesResult>.Success>().Subject.Value;
        value.BlockedDates.Should().Equal(new DateOnly(2026, 8, 3));
        value.PartialDates.Should().BeEmpty();
    }

    [Fact]
    public async Task A_morning_half_day_leaves_the_afternoon_only_partially_blocked()
    {
        var existing = LeaveRequest.Create(
            Employee, LeaveType.Annual,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "acara pagi", null, halfDay: true, halfDayPeriod: HalfDayPeriod.Morning,
            startHour: null, endHour: null, Guid.NewGuid(), Now);
        existing.Approve(Guid.NewGuid(), "Owner Utama", Now);
        Approved(existing);

        // Candidate is an afternoon Izin — does not intersect the approved morning half day.
        var result = await GetBlockedLeaveDatesHandler.Handle(
            new GetBlockedLeaveDatesQuery(
                Employee.Value, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 3),
                HalfDay: false, HalfDayPeriod: null, StartHour: 14, EndHour: 16, _caller),
            _leaveRequests, TestPolicies.Standard, CancellationToken.None);

        var value = result.Should().BeOfType<Result<BlockedLeaveDatesResult>.Success>().Subject.Value;
        value.BlockedDates.Should().BeEmpty();
        value.PartialDates.Should().Equal(new DateOnly(2026, 8, 3));
    }

    [Fact]
    public async Task An_afternoon_candidate_is_blocked_by_an_overlapping_afternoon_izin()
    {
        var existing = LeaveRequest.Create(
            Employee, LeaveType.Permission,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "izin", null, halfDay: false, halfDayPeriod: null, startHour: 14, endHour: 17,
            Guid.NewGuid(), Now);
        existing.Approve(Guid.NewGuid(), "Owner Utama", Now);
        Approved(existing);

        var result = await GetBlockedLeaveDatesHandler.Handle(
            new GetBlockedLeaveDatesQuery(
                Employee.Value, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 3),
                HalfDay: true, HalfDayPeriod: HalfDayPeriod.Afternoon, StartHour: null, EndHour: null, _caller),
            _leaveRequests, TestPolicies.Standard, CancellationToken.None);

        var value = result.Should().BeOfType<Result<BlockedLeaveDatesResult>.Success>().Subject.Value;
        value.BlockedDates.Should().Equal(new DateOnly(2026, 8, 3));
        value.PartialDates.Should().BeEmpty();
    }

    [Fact]
    public async Task Nothing_chosen_yet_is_priced_as_a_full_day_so_any_approved_leave_blocks()
    {
        var existing = LeaveRequest.Create(
            Employee, LeaveType.Annual,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "acara pagi", null, halfDay: true, halfDayPeriod: HalfDayPeriod.Morning,
            startHour: null, endHour: null, Guid.NewGuid(), Now);
        existing.Approve(Guid.NewGuid(), "Owner Utama", Now);
        Approved(existing);

        var result = await GetBlockedLeaveDatesHandler.Handle(
            new GetBlockedLeaveDatesQuery(
                Employee.Value, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 3),
                HalfDay: false, HalfDayPeriod: null, StartHour: null, EndHour: null, _caller),
            _leaveRequests, TestPolicies.Standard, CancellationToken.None);

        var value = result.Should().BeOfType<Result<BlockedLeaveDatesResult>.Success>().Subject.Value;
        value.BlockedDates.Should().Equal(new DateOnly(2026, 8, 3));
    }

    [Fact]
    public async Task An_oversized_window_is_rejected_before_the_per_date_loop_runs()
    {
        // Client-controlled From/To with no other size limit — this is what keeps a
        // 0001-01-01..9999-12-31 request from spinning millions of loop iterations.
        var result = await GetBlockedLeaveDatesHandler.Handle(
            new GetBlockedLeaveDatesQuery(
                Employee.Value, new DateOnly(2020, 1, 1), new DateOnly(2030, 1, 1),
                HalfDay: false, HalfDayPeriod: null, StartHour: null, EndHour: null, _caller),
            _leaveRequests, TestPolicies.Standard, CancellationToken.None);

        result.Should().BeOfType<Result<BlockedLeaveDatesResult>.Error>()
            .Which.Code.Should().Be("leave.date_range_too_wide");
    }

    [Fact]
    public async Task A_window_at_the_cap_is_accepted()
    {
        _leaveRequests.ListAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveRequest>());

        var result = await GetBlockedLeaveDatesHandler.Handle(
            new GetBlockedLeaveDatesQuery(
                Employee.Value, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1).AddDays(1100),
                HalfDay: false, HalfDayPeriod: null, StartHour: null, EndHour: null, _caller),
            _leaveRequests, TestPolicies.Standard, CancellationToken.None);

        result.Should().BeOfType<Result<BlockedLeaveDatesResult>.Success>();
    }

    private static LeaveRequest ApprovedFullDay(LocalDate start, LocalDate end)
    {
        var request = LeaveRequest.Create(
            Employee, LeaveType.Sick, start, end, "sakit", TestAttachments.DoctorsNote(),
            halfDay: false, halfDayPeriod: null, startHour: null, endHour: null, Guid.NewGuid(), Now);
        request.Approve(Guid.NewGuid(), "Owner Utama", Now);
        return request;
    }
}
