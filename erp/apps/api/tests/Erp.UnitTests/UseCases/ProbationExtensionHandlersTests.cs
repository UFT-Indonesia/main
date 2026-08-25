using Ardalis.Specification;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Probation;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Probation.Common;
using Erp.UseCases.Probation.CreateProbationExtensionRequest;
using Erp.UseCases.Probation.DecideProbationExtensionRequest;
using FluentAssertions;
using NodaTime;
using NSubstitute;
using Wolverine;

namespace Erp.UnitTests.UseCases;

public class ProbationExtensionHandlersTests
{
    // 14 Jul 2026, mid-morning in Jakarta.
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 14, 3, 0);

    private readonly IRepository<Employee> _employees = Substitute.For<IRepository<Employee>>();
    private readonly IReadRepository<Employee> _employeesRead = Substitute.For<IReadRepository<Employee>>();
    private readonly IRepository<ProbationExtensionRequest> _requests =
        Substitute.For<IRepository<ProbationExtensionRequest>>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    private readonly Employee _owner;
    private readonly Employee _manager;
    private readonly Employee _staff;
    private readonly Employee _otherStaff;

    private readonly Caller _ownerCaller;
    private readonly Caller _managerCaller;
    private readonly Caller _staffCaller;

    public ProbationExtensionHandlersTests()
    {
        _clock.GetCurrentInstant().Returns(Now);

        _owner = NewEmployee("Owner Utama", EmployeeRole.Owner, null, "3201234567890123");
        _manager = NewEmployee("Manager Satu", EmployeeRole.Manager, _owner.Id, "3201234567890124");

        // Hired 1 Jun 2026 → probation ends 1 Sep 2026, so still on probation "today".
        _staff = NewEmployee(
            "Staff Baru", EmployeeRole.Staff, _manager.Id, "3201234567890125", new LocalDate(2026, 6, 1));
        _otherStaff = NewEmployee(
            "Staff Lain", EmployeeRole.Staff, _owner.Id, "3201234567890126", new LocalDate(2026, 6, 1));

        foreach (var employee in new[] { _owner, _manager, _staff, _otherStaff })
        {
            _employees.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
            _employeesRead.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        }

        _ownerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Owner, _owner.Id, "Owner Utama");
        _managerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Manager, _manager.Id, "Manager Satu");
        _staffCaller = new Caller(Guid.NewGuid(), EmployeeRole.Staff, _staff.Id, "Staff Baru");

        _requests.AnyAsync(Arg.Any<ISpecification<ProbationExtensionRequest>>(), Arg.Any<CancellationToken>())
            .Returns(false);
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

    private Task<Result<ProbationExtensionResult>> FileAsync(
        Employee subject, Caller caller, DateOnly? proposed = null) =>
        CreateProbationExtensionRequestHandler.Handle(
            new CreateProbationExtensionRequestCommand(
                subject.Id.Value,
                proposed ?? new DateOnly(2026, 11, 1),
                "Perlu waktu tambahan untuk penilaian.",
                caller),
            _employeesRead,
            _requests,
            _clock,
            CancellationToken.None);

    private ProbationExtensionRequest PendingFor(Employee subject, Guid requestedByUserId)
    {
        var request = ProbationExtensionRequest.Create(
            subject.Id,
            subject.ProbationEndsOn!.Value,
            new LocalDate(2026, 11, 1),
            "Perlu waktu tambahan.",
            requestedByUserId,
            Now);
        _requests.FirstOrDefaultAsync(
                Arg.Any<ISpecification<ProbationExtensionRequest>>(), Arg.Any<CancellationToken>())
            .Returns(request);
        return request;
    }

    // ---- filing ---------------------------------------------------------

    [Fact]
    public async Task Manager_can_file_for_their_own_staff_on_probation()
    {
        var result = await FileAsync(_staff, _managerCaller);

        var success = result.Should().BeOfType<Result<ProbationExtensionResult>.Success>().Subject;
        success.Value.Status.Should().Be("Pending");
        success.Value.CurrentEndsOn.Should().Be(new DateOnly(2026, 9, 1));
        success.Value.ProposedEndsOn.Should().Be(new DateOnly(2026, 11, 1));
        await _requests.Received(1).AddAsync(Arg.Any<ProbationExtensionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manager_cannot_file_for_someone_elses_staff()
    {
        var result = await FileAsync(_otherStaff, _managerCaller);

        result.Should().BeOfType<Result<ProbationExtensionResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    [Fact]
    public async Task An_owner_never_files_they_edit_the_date_directly()
    {
        var result = await FileAsync(_staff, _ownerCaller);

        result.Should().BeOfType<Result<ProbationExtensionResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    [Fact]
    public async Task Staff_cannot_file_about_themselves()
    {
        var result = await FileAsync(_staff, _staffCaller);

        result.Should().BeOfType<Result<ProbationExtensionResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    [Fact]
    public async Task Cannot_file_for_someone_already_confirmed()
    {
        var confirmed = NewEmployee(
            "Staff Lama", EmployeeRole.Staff, _manager.Id, "3201234567890127", new LocalDate(2025, 1, 1));
        _employeesRead.GetByIdAsync(confirmed.Id, Arg.Any<CancellationToken>()).Returns(confirmed);

        var result = await FileAsync(confirmed, _managerCaller);

        result.Should().BeOfType<Result<ProbationExtensionResult>.Error>()
            .Which.Code.Should().Be("probation.already_confirmed");
    }

    [Fact]
    public async Task The_proposed_date_must_actually_be_later()
    {
        var result = await FileAsync(_staff, _managerCaller, proposed: new DateOnly(2026, 8, 1));

        result.Should().BeOfType<Result<ProbationExtensionResult>.Error>()
            .Which.Code.Should().Be("probation.not_an_extension");
    }

    [Fact]
    public async Task Only_one_request_may_be_open_at_a_time()
    {
        _requests.AnyAsync(Arg.Any<ISpecification<ProbationExtensionRequest>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await FileAsync(_staff, _managerCaller);

        result.Should().BeOfType<Result<ProbationExtensionResult>.Error>()
            .Which.Code.Should().Be("probation.pending_exists");
    }

    // ---- deciding -------------------------------------------------------

    [Fact]
    public async Task Approval_writes_the_exact_date_that_was_asked_for()
    {
        var request = PendingFor(_staff, _managerCaller.UserId);

        var result = await ApproveProbationExtensionHandler.Handle(
            new ApproveProbationExtensionCommand(request.Id.Value, _ownerCaller, null),
            _requests, _employees, _clock, _bus, CancellationToken.None);

        result.Should().BeOfType<Result<ProbationExtensionResult>.Success>();
        request.Status.Should().Be(ProbationExtensionStatus.Approved);
        _staff.ProbationEndsOnOverride.Should().Be(new LocalDate(2026, 11, 1));
        _staff.ProbationEndsOn.Should().Be(new LocalDate(2026, 11, 1));
    }

    [Fact]
    public async Task A_manager_cannot_approve_their_own_request()
    {
        var request = PendingFor(_staff, _managerCaller.UserId);

        var result = await ApproveProbationExtensionHandler.Handle(
            new ApproveProbationExtensionCommand(request.Id.Value, _managerCaller, null),
            _requests, _employees, _clock, _bus, CancellationToken.None);

        result.Should().BeOfType<Result<ProbationExtensionResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
        _staff.ProbationEndsOnOverride.Should().BeNull();
    }

    [Fact]
    public async Task A_request_that_went_stale_cannot_be_approved()
    {
        var request = PendingFor(_staff, _managerCaller.UserId);

        // Probation lapsed while the request waited: approving now would retroactively
        // un-confirm someone whose annual leave may already have been granted in the gap.
        _clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 9, 2, 3, 0));

        var result = await ApproveProbationExtensionHandler.Handle(
            new ApproveProbationExtensionCommand(request.Id.Value, _ownerCaller, null),
            _requests, _employees, _clock, _bus, CancellationToken.None);

        result.Should().BeOfType<Result<ProbationExtensionResult>.Error>()
            .Which.Code.Should().Be("probation.already_confirmed");
        _staff.ProbationEndsOnOverride.Should().BeNull();
    }

    [Fact]
    public async Task A_stale_request_can_still_be_denied_to_clear_it_out()
    {
        var request = PendingFor(_staff, _managerCaller.UserId);
        _clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 9, 2, 3, 0));

        var result = await DenyProbationExtensionHandler.Handle(
            new DenyProbationExtensionCommand(request.Id.Value, _ownerCaller, "Sudah lulus."),
            _requests, _employees, _clock, _bus, CancellationToken.None);

        result.Should().BeOfType<Result<ProbationExtensionResult>.Success>();
        request.Status.Should().Be(ProbationExtensionStatus.Denied);
    }

    [Fact]
    public async Task Only_the_filer_may_withdraw()
    {
        var request = PendingFor(_staff, _managerCaller.UserId);

        var byOwner = await CancelProbationExtensionHandler.Handle(
            new CancelProbationExtensionCommand(request.Id.Value, _ownerCaller, null),
            _requests, _employees, _clock, _bus, CancellationToken.None);
        byOwner.Should().BeOfType<Result<ProbationExtensionResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);

        var byFiler = await CancelProbationExtensionHandler.Handle(
            new CancelProbationExtensionCommand(request.Id.Value, _managerCaller, null),
            _requests, _employees, _clock, _bus, CancellationToken.None);
        byFiler.Should().BeOfType<Result<ProbationExtensionResult>.Success>();
        request.Status.Should().Be(ProbationExtensionStatus.Cancelled);
    }
}
