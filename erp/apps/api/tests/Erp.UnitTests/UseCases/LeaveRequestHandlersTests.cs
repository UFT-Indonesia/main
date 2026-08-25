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
using Erp.UseCases.Leave.DecideLeaveRequest;
using FluentAssertions;
using NodaTime;
using NSubstitute;
using Wolverine;

namespace Erp.UnitTests.UseCases;

public class LeaveRequestHandlersTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 14, 8, 0);

    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();
    private readonly IRepository<LeaveRequest> _leaveRequests = Substitute.For<IRepository<LeaveRequest>>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    private readonly Employee _owner;
    private readonly Employee _manager;
    private readonly Employee _staff;

    private readonly Caller _ownerCaller;
    private readonly Caller _managerCaller;
    private readonly Caller _staffCaller;

    public LeaveRequestHandlersTests()
    {
        _clock.GetCurrentInstant().Returns(Now);

        _owner = NewEmployee("Owner Utama", EmployeeRole.Owner, null, "3201234567890123");
        _manager = NewEmployee("Manager Satu", EmployeeRole.Manager, _owner.Id, "3201234567890124");
        _staff = NewEmployee("Staff Biasa", EmployeeRole.Staff, _manager.Id, "3201234567890125");

        foreach (var employee in new[] { _owner, _manager, _staff })
        {
            _employees.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        }

        _ownerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Owner, _owner.Id, "Owner Utama");
        _managerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Manager, _manager.Id, "Manager Satu");
        _staffCaller = new Caller(Guid.NewGuid(), EmployeeRole.Staff, _staff.Id, "Staff Biasa");

        _leaveRequests.AnyAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Nothing approved yet, so the quota check has a clean slate for every case here.
        _leaveRequests.ListAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveRequest>());
    }

    private static Employee NewEmployee(string name, EmployeeRole role, EmployeeId? parentId, string nik) =>
        Employee.Create(
            name,
            Nik.Create(nik),
            Money.Idr(5_000_000m),
            new LocalDate(2026, 1, 1),
            role,
            parentId);

    private CreateLeaveRequestCommand CommandFor(Employee subject, Caller caller) => new(
        subject.Id.Value,
        "Annual",
        new DateOnly(2026, 8, 3),
        new DateOnly(2026, 8, 7),
        "acara keluarga",
        caller);

    private Task<Result<LeaveRequestResult>> CreateAsync(Employee subject, Caller caller) =>
        CreateLeaveRequestHandler.Handle(
            CommandFor(subject, caller), _employees, _leaveRequests, _clock, _bus, CancellationToken.None);

    private LeaveRequest PendingFor(Employee subject, Guid requestedByUserId)
    {
        var request = LeaveRequest.Create(
            subject.Id, LeaveType.Annual,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 7),
            null, requestedByUserId, Now);
        _leaveRequests.FirstOrDefaultAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(request);
        return request;
    }

    // ---- creation scope -------------------------------------------------

    [Fact]
    public async Task Staff_can_file_their_own_leave()
    {
        var result = await CreateAsync(_staff, _staffCaller);

        var success = result.Should().BeOfType<Result<LeaveRequestResult>.Success>().Subject;
        success.Value.Status.Should().Be("Pending");
        success.Value.WorkdayCount.Should().Be(5);
        await _leaveRequests.Received(1).AddAsync(Arg.Any<LeaveRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Staff_cannot_file_for_someone_else()
    {
        var otherStaff = NewEmployee("Staff Lain", EmployeeRole.Staff, _manager.Id, "3201234567890126");
        _employees.GetByIdAsync(otherStaff.Id, Arg.Any<CancellationToken>()).Returns(otherStaff);

        var result = await CreateAsync(otherStaff, _staffCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
        await _leaveRequests.DidNotReceive().AddAsync(Arg.Any<LeaveRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manager_can_file_for_their_own_staff()
    {
        var result = await CreateAsync(_staff, _managerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
    }

    [Fact]
    public async Task Manager_cannot_file_for_another_managers_staff()
    {
        var foreignStaff = NewEmployee("Staff Asing", EmployeeRole.Staff, EmployeeId.New(), "3201234567890127");
        _employees.GetByIdAsync(foreignStaff.Id, Arg.Any<CancellationToken>()).Returns(foreignStaff);

        var result = await CreateAsync(foreignStaff, _managerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    [Fact]
    public async Task Manager_cannot_file_on_behalf_of_the_owner()
    {
        var result = await CreateAsync(_owner, _managerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    [Fact]
    public async Task Owner_leave_is_approved_on_creation()
    {
        var result = await CreateAsync(_owner, _ownerCaller);

        var success = result.Should().BeOfType<Result<LeaveRequestResult>.Success>().Subject;
        success.Value.Status.Should().Be("Approved");
        success.Value.DecidedByName.Should().Be("Owner Utama");
        success.Value.DecidedAtUtc.Should().Be(Now.ToDateTimeOffset());
    }

    [Fact]
    public async Task Manager_leave_still_starts_pending()
    {
        var managerCallerForSelf = _managerCaller;

        var result = await CreateAsync(_manager, managerCallerForSelf);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>()
            .Which.Value.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Create_rejects_invalid_type()
    {
        var command = CommandFor(_staff, _staffCaller) with { Type = "Vacation" };

        var result = await CreateLeaveRequestHandler.Handle(
            command, _employees, _leaveRequests, _clock, _bus, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.type");
    }

    [Fact]
    public async Task Create_returns_not_found_for_unknown_employee()
    {
        var command = CommandFor(_staff, _staffCaller) with { EmployeeId = Guid.NewGuid() };

        var result = await CreateLeaveRequestHandler.Handle(
            command, _employees, _leaveRequests, _clock, _bus, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.NotFound>();
    }

    [Fact]
    public async Task Create_rejects_when_pending_request_exists()
    {
        _leaveRequests.AnyAsync(Arg.Any<PendingLeaveForEmployeeSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateAsync(_staff, _staffCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.pending_exists");
        await _leaveRequests.DidNotReceive().AddAsync(Arg.Any<LeaveRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_overlap_with_approved_leave()
    {
        _leaveRequests.AnyAsync(Arg.Any<ApprovedLeaveOverlappingSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateAsync(_staff, _staffCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.overlaps_approved");
    }

    [Fact]
    public async Task Create_rejects_a_terminated_employee()
    {
        var terminated = NewEmployee("Sudah Keluar", EmployeeRole.Staff, _manager.Id, "3201234567890128");
        terminated.Terminate(new LocalDate(2026, 7, 1));
        _employees.GetByIdAsync(terminated.Id, Arg.Any<CancellationToken>()).Returns(terminated);

        var result = await CreateAsync(terminated, _ownerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.employee_terminated");
        await _leaveRequests.DidNotReceive().AddAsync(Arg.Any<LeaveRequest>(), Arg.Any<CancellationToken>());
    }

    // ---- decision scope -------------------------------------------------

    private Task<Result<LeaveRequestResult>> ApproveAsync(LeaveRequest request, Caller caller) =>
        ApproveLeaveRequestHandler.Handle(
            new ApproveLeaveRequestCommand(request.Id.Value, caller),
            _leaveRequests, _employees, _clock, _bus, CancellationToken.None);

    [Fact]
    public async Task A_managers_own_staff_leave_is_approvable_by_that_manager()
    {
        var request = PendingFor(_staff, requestedByUserId: _staffCaller.UserId);

        var result = await ApproveAsync(request, _managerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>()
            .Which.Value.Status.Should().Be("Approved");
        request.DecidedByName.Should().Be("Manager Satu");
    }

    [Fact]
    public async Task Another_managers_staff_leave_is_not_approvable()
    {
        var foreignStaff = NewEmployee("Staff Asing", EmployeeRole.Staff, EmployeeId.New(), "3201234567890129");
        _employees.GetByIdAsync(foreignStaff.Id, Arg.Any<CancellationToken>()).Returns(foreignStaff);
        var request = PendingFor(foreignStaff, requestedByUserId: Guid.NewGuid());

        var result = await ApproveAsync(request, _managerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
        await _leaveRequests.DidNotReceive().UpdateAsync(Arg.Any<LeaveRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Whoever_filed_the_request_cannot_approve_it()
    {
        // The manager filed for their own staff, so only an Owner can authorize it.
        var request = PendingFor(_staff, requestedByUserId: _managerCaller.UserId);

        var byFiler = await ApproveAsync(request, _managerCaller);
        byFiler.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);

        var byOwner = await ApproveAsync(request, _ownerCaller);
        byOwner.Should().BeOfType<Result<LeaveRequestResult>.Success>();
    }

    [Fact]
    public async Task A_manager_cannot_approve_their_own_leave()
    {
        var request = PendingFor(_manager, requestedByUserId: _managerCaller.UserId);

        var result = await ApproveAsync(request, _managerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    [Fact]
    public async Task A_managers_leave_is_approvable_by_the_owner()
    {
        var request = PendingFor(_manager, requestedByUserId: _managerCaller.UserId);

        var result = await ApproveAsync(request, _ownerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
    }

    [Fact]
    public async Task Staff_cannot_approve_anything()
    {
        var request = PendingFor(_staff, requestedByUserId: _staffCaller.UserId);

        var result = await ApproveAsync(request, _staffCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    [Fact]
    public async Task Decide_returns_not_found_for_missing_request()
    {
        _leaveRequests.FirstOrDefaultAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns((LeaveRequest?)null);

        var result = await DenyLeaveRequestHandler.Handle(
            new DenyLeaveRequestCommand(Guid.NewGuid(), _ownerCaller, null),
            _leaveRequests, _employees, _clock, _bus, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.NotFound>();
    }

    // ---- cancellation ---------------------------------------------------

    private Task<Result<LeaveRequestResult>> CancelAsync(LeaveRequest request, Caller caller, string? note) =>
        CancelLeaveRequestHandler.Handle(
            new CancelLeaveRequestCommand(request.Id.Value, caller, note),
            _leaveRequests, _employees, _clock, _bus, CancellationToken.None);

    [Fact]
    public async Task The_subject_can_cancel_their_own_approved_leave()
    {
        var request = PendingFor(_staff, requestedByUserId: _staffCaller.UserId);
        request.Approve(_managerCaller.UserId, "Manager Satu", Now);

        var result = await CancelAsync(request, _staffCaller, "sembuh lebih cepat");

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>()
            .Which.Value.Status.Should().Be("Cancelled");
        request.DecisionNote.Should().Be("sembuh lebih cepat");
    }

    [Fact]
    public async Task A_manager_can_cancel_their_own_staffs_leave()
    {
        var request = PendingFor(_staff, requestedByUserId: _staffCaller.UserId);

        var result = await CancelAsync(request, _managerCaller, null);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
    }

    [Fact]
    public async Task An_unrelated_manager_cannot_cancel()
    {
        var outsider = new Caller(Guid.NewGuid(), EmployeeRole.Manager, EmployeeId.New(), "Manager Lain");
        var request = PendingFor(_staff, requestedByUserId: _staffCaller.UserId);

        var result = await CancelAsync(request, outsider, null);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    [Fact]
    public async Task Only_the_owner_themselves_can_cancel_their_auto_approved_leave()
    {
        var request = PendingFor(_owner, requestedByUserId: _ownerCaller.UserId);
        request.Approve(_ownerCaller.UserId, "Owner Utama", Now);

        var byManager = await CancelAsync(request, _managerCaller, null);
        byManager.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);

        var bySelf = await CancelAsync(request, _ownerCaller, null);
        bySelf.Should().BeOfType<Result<LeaveRequestResult>.Success>()
            .Which.Value.Status.Should().Be("Cancelled");
    }
}
