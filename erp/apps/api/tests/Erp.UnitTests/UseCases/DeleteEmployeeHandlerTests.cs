using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.DeleteEmployee;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class DeleteEmployeeHandlerTests
{
    private readonly IRepository<Employee> _employees = Substitute.For<IRepository<Employee>>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private DeleteEmployeeHandler Sut() => new(_employees, _clock);

    private static Employee NewOwner()
    {
        return Employee.Create(
            "Owner",
            Nik.Create("3201234567890123"),
            Money.Idr(8_000_000m),
            new LocalDate(2025, 1, 1),
            EmployeeRole.Owner);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_missing()
    {
        _employees.GetByIdAsync(Arg.Any<EmployeeId>(), Arg.Any<CancellationToken>())
            .Returns((Employee?)null);

        var result = await Sut().Handle(
            new DeleteEmployeeCommand(Guid.NewGuid(), null),
            CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.NotFound>();
    }

    [Fact]
    public async Task Handle_terminates_employee_with_provided_date()
    {
        var owner = NewOwner();
        _employees.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await Sut().Handle(
            new DeleteEmployeeCommand(owner.Id.Value, new DateOnly(2025, 6, 1)),
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<EmployeeResult>.Success>().Subject;
        success.Value.Status.Should().Be("Terminated");
        success.Value.TerminationDate.Should().Be(new DateOnly(2025, 6, 1));
        owner.DomainEvents.OfType<EmployeeTerminated>().Should().HaveCount(1);
        await _employees.Received(1).UpdateAsync(owner, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_uses_clock_when_termination_date_omitted()
    {
        var owner = NewOwner();
        _employees.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);
        _clock.GetCurrentInstant().Returns(
            Instant.FromUtc(2025, 7, 15, 0, 0));

        var result = await Sut().Handle(
            new DeleteEmployeeCommand(owner.Id.Value, null),
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

        var result = await Sut().Handle(
            new DeleteEmployeeCommand(owner.Id.Value, new DateOnly(2025, 6, 1)),
            CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.Error>()
            .Which.Code.Should().Be("employee.already_terminated");
        await _employees.DidNotReceive().UpdateAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
    }
}
