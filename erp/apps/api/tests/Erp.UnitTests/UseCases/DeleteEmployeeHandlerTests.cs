using Ardalis.Specification;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.DeleteEmployee;
using FluentAssertions;
using NodaTime;
using NSubstitute;
using Wolverine;

namespace Erp.UnitTests.UseCases;

public class DeleteEmployeeHandlerTests
{
    // Every employee command carries the caller now — it lands on the audit row.
    private static readonly Caller Actor =
        new(Guid.NewGuid(), EmployeeRole.Owner, EmployeeId.New(), "Owner Utama");

    private readonly IRepository<Employee> _employees = Substitute.For<IRepository<Employee>>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    public DeleteEmployeeHandlerTests()
    {
        // Nobody reports to the employee unless a test says otherwise.
        _employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private static Employee NewOwner()
    {
        return Employee.Create(
            "Owner",
            Nik.Create("3201234567890123"),
            Money.Idr(8_000_000m),
            new LocalDate(2025, 1, 1),
            EmployeeRole.Owner);
    }

    private static Employee NewReport(string fullName, EmployeeId parentId)
    {
        return Employee.Create(
            fullName,
            Nik.Create("3201234567890124"),
            Money.Idr(5_000_000m),
            new LocalDate(2025, 1, 1),
            EmployeeRole.Staff,
            parentId);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_missing()
    {
        _employees.GetByIdAsync(Arg.Any<EmployeeId>(), Arg.Any<CancellationToken>())
            .Returns((Employee?)null);

        var result = await DeleteEmployeeHandler.Handle(
            new DeleteEmployeeCommand(Guid.NewGuid(), null, Actor),
            _employees,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.NotFound>();
    }

    [Fact]
    public async Task Handle_terminates_employee_with_provided_date()
    {
        var owner = NewOwner();
        _employees.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await DeleteEmployeeHandler.Handle(
            new DeleteEmployeeCommand(owner.Id.Value, new DateOnly(2025, 6, 1), Actor),
            _employees,
            _clock,
            _bus,
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<EmployeeResult>.Success>().Subject;
        success.Value.Status.Should().Be("Terminated");
        success.Value.TerminationDate.Should().Be(new DateOnly(2025, 6, 1));
        owner.DomainEvents.OfType<EmployeeTerminated>().Should().HaveCount(1);
        await _employees.Received(1).UpdateAsync(owner, Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(Arg.Any<EmployeeTerminated>(), Arg.Any<DeliveryOptions>());
    }

    [Fact]
    public async Task Handle_uses_clock_when_termination_date_omitted()
    {
        var owner = NewOwner();
        _employees.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);
        _clock.GetCurrentInstant().Returns(
            Instant.FromUtc(2025, 7, 15, 0, 0));

        var result = await DeleteEmployeeHandler.Handle(
            new DeleteEmployeeCommand(owner.Id.Value, null, Actor),
            _employees,
            _clock,
            _bus,
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<EmployeeResult>.Success>().Subject;
        success.Value.TerminationDate.Should().Be(new DateOnly(2025, 7, 15));
    }

    [Fact]
    public async Task Handle_returns_error_when_already_terminated()
    {
        var owner = NewOwner();
        owner.Terminate(new LocalDate(2025, 5, 1));
        _employees.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await DeleteEmployeeHandler.Handle(
            new DeleteEmployeeCommand(owner.Id.Value, new DateOnly(2025, 6, 1), Actor),
            _employees,
            _clock,
            _bus,
            CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.Error>()
            .Which.Code.Should().Be("employee.already_terminated");
        await _employees.DidNotReceive().UpdateAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_rejects_termination_while_active_direct_reports_remain()
    {
        var manager = NewOwner();
        _employees.GetByIdAsync(manager.Id, Arg.Any<CancellationToken>()).Returns(manager);
        _employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns([NewReport("Budi", manager.Id), NewReport("Siti", manager.Id)]);

        var result = await DeleteEmployeeHandler.Handle(
            new DeleteEmployeeCommand(manager.Id.Value, new DateOnly(2025, 6, 1), Actor),
            _employees,
            _clock,
            _bus,
            CancellationToken.None);

        var error = result.Should().BeOfType<Result<EmployeeResult>.Error>().Subject;
        error.Code.Should().Be("employee.has_active_reports");
        error.Message.Should().Contain("Budi").And.Contain("Siti");
        await _employees.DidNotReceive().UpdateAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
    }
}
