using Ardalis.Specification;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.CreateLeaveRequest;
using FluentAssertions;
using NodaTime;
using NSubstitute;
using Wolverine;

namespace Erp.UnitTests.UseCases;

/// <summary>
/// The quota check as the filing path actually exercises it. The pure entitlement maths lives in
/// <see cref="LeaveQuotaTests"/>; this is about what a request gets rejected with.
/// </summary>
public class LeaveQuotaEnforcementTests
{
    // 1 Oct 2026, mid-morning in Jakarta.
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 1, 3, 0);

    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();
    private readonly IRepository<LeaveRequest> _leaveRequests = Substitute.For<IRepository<LeaveRequest>>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    private readonly Employee _owner;
    private readonly Employee _manager;
    private readonly Employee _staff;
    private readonly Caller _managerCaller;
    private readonly Caller _ownerCaller;

    public LeaveQuotaEnforcementTests()
    {
        _clock.GetCurrentInstant().Returns(Now);

        _owner = NewEmployee("Owner Utama", EmployeeRole.Owner, null, "3201234567890123");
        _manager = NewEmployee("Manager Satu", EmployeeRole.Manager, _owner.Id, "3201234567890124");
        // Hired 1 Jan 2026 → confirmed 1 Apr 2026 → 12 - 4 = 8 annual days for 2026.
        _staff = NewEmployee(
            "Staff Biasa", EmployeeRole.Staff, _manager.Id, "3201234567890125", new LocalDate(2026, 1, 1));

        foreach (var employee in new[] { _owner, _manager, _staff })
        {
            _employees.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        }

        _ownerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Owner, _owner.Id, "Owner Utama");
        _managerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Manager, _manager.Id, "Manager Satu");

        _leaveRequests.AnyAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(false);
        Approved();
    }

    private static Employee NewEmployee(
        string name, EmployeeRole role, EmployeeId? parentId, string nik, LocalDate? hireDate = null) =>
        Employee.Create(
            name,
            Nik.Create(nik),
            Money.Idr(5_000_000m),
            new LocalDate(2026, 1, 1),
            role,
            parentId,
            hireDate: hireDate);

    private void Approved(params LeaveRequest[] requests) =>
        _leaveRequests.ListAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(requests.ToList());

    private static LeaveRequest ApprovedLeave(Employee subject, LeaveType type, LocalDate start, LocalDate end)
    {
        var request = LeaveRequest.Create(subject.Id, type, start, end, null, Guid.NewGuid(), Now);
        request.Approve(Guid.NewGuid(), "Owner Utama", Now);
        return request;
    }

    private Task<Result<LeaveRequestResult>> FileAsync(
        Employee subject, LeaveType type, DateOnly start, DateOnly end, Caller? caller = null) =>
        CreateLeaveRequestHandler.Handle(
            new CreateLeaveRequestCommand(
                subject.Id.Value, type.ToString(), start, end, "alasan", caller ?? _managerCaller),
            _employees, _leaveRequests, _clock, _bus, CancellationToken.None);

    [Fact]
    public async Task Probation_blocks_annual_leave()
    {
        var probationer = NewEmployee(
            "Staff Baru", EmployeeRole.Staff, _manager.Id, "3201234567890126", new LocalDate(2026, 9, 1));
        _employees.GetByIdAsync(probationer.Id, Arg.Any<CancellationToken>()).Returns(probationer);

        var result = await FileAsync(
            probationer, LeaveType.Annual, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6));

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.probation_annual");
    }

    [Fact]
    public async Task Probation_does_not_block_sick_leave()
    {
        var probationer = NewEmployee(
            "Staff Baru", EmployeeRole.Staff, _manager.Id, "3201234567890126", new LocalDate(2026, 9, 1));
        _employees.GetByIdAsync(probationer.Id, Arg.Any<CancellationToken>()).Returns(probationer);

        var result = await FileAsync(
            probationer, LeaveType.Sick, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6));

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
    }

    [Fact]
    public async Task A_request_past_the_remaining_days_is_rejected_by_the_number()
    {
        // 8 annual days for 2026; 6 already taken, so 2 remain.
        Approved(ApprovedLeave(_staff, LeaveType.Annual, new LocalDate(2026, 5, 4), new LocalDate(2026, 5, 11)));

        var result = await FileAsync(
            _staff, LeaveType.Annual, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9));

        var error = result.Should().BeOfType<Result<LeaveRequestResult>.Error>().Subject;
        error.Code.Should().Be("leave.quota_exceeded");
        error.Message.Should().Contain("2").And.Contain("2026").And.Contain("5");
    }

    [Fact]
    public async Task A_request_that_exactly_fits_is_allowed()
    {
        Approved(ApprovedLeave(_staff, LeaveType.Annual, new LocalDate(2026, 5, 4), new LocalDate(2026, 5, 11)));

        var result = await FileAsync(
            _staff, LeaveType.Annual, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6));

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
    }

    [Fact]
    public async Task Days_of_another_type_do_not_eat_the_annual_quota()
    {
        Approved(ApprovedLeave(_staff, LeaveType.Sick, new LocalDate(2026, 5, 4), new LocalDate(2026, 5, 15)));

        var result = await FileAsync(
            _staff, LeaveType.Annual, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9));

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
    }

    [Fact]
    public async Task A_request_over_new_year_must_fit_the_far_side_too()
    {
        // 2027 is a full 12 days and 2026 has 8, but the 2026 half is what runs out first.
        Approved(ApprovedLeave(_staff, LeaveType.Annual, new LocalDate(2026, 5, 4), new LocalDate(2026, 5, 13)));

        // Mon 28 Dec 2026 – Fri 8 Jan 2027: 4 workdays in 2026, 6 in 2027.
        var result = await FileAsync(
            _staff, LeaveType.Annual, new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 8));

        var error = result.Should().BeOfType<Result<LeaveRequestResult>.Error>().Subject;
        error.Code.Should().Be("leave.quota_exceeded");
        error.Message.Should().Contain("2026");
    }

    [Fact]
    public async Task An_owners_leave_is_never_capped()
    {
        _owner.SetLeaveQuota(LeaveType.Annual, 1);
        Approved(ApprovedLeave(_owner, LeaveType.Annual, new LocalDate(2026, 5, 4), new LocalDate(2026, 5, 29)));

        var result = await FileAsync(
            _owner, LeaveType.Annual, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 30), _ownerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
    }

    [Fact]
    public async Task An_override_replaces_the_prorated_figure()
    {
        _staff.SetLeaveQuota(LeaveType.Annual, 15);
        Approved(ApprovedLeave(_staff, LeaveType.Annual, new LocalDate(2026, 5, 4), new LocalDate(2026, 5, 15)));

        // 15 entitled, 10 taken — a 5-day request is the last that fits.
        var result = await FileAsync(
            _staff, LeaveType.Annual, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9));

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
    }

    [Fact]
    public async Task A_zero_override_blocks_that_type_outright()
    {
        _staff.SetLeaveQuota(LeaveType.Permission, 0);

        var result = await FileAsync(
            _staff, LeaveType.Permission, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5));

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.quota_exceeded");
    }
}
