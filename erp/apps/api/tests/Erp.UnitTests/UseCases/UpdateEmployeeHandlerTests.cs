using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.UpdateEmployee;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class UpdateEmployeeHandlerTests
{
    private readonly IRepository<Employee> _employees = Substitute.For<IRepository<Employee>>();
    private readonly IEmployeeHierarchyLookup _hierarchy = Substitute.For<IEmployeeHierarchyLookup>();

    public UpdateEmployeeHandlerTests()
    {
        _hierarchy.ExistsAsync(Arg.Any<EmployeeId>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _hierarchy.GetAncestorsAsync(Arg.Any<EmployeeId>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EmployeeId>());
    }

    private static Employee NewOwner()
    {
        return Employee.Create(
            "Owner Lama",
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

        var result = await UpdateEmployeeHandler.Handle(
            new UpdateEmployeeCommand(
                Guid.NewGuid(),
                "Owner Baru",
                null,
                8_000_000m,
                new DateOnly(2025, 1, 1),
                "Owner",
                null),
            _employees,
            _hierarchy,
            CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.NotFound>();
    }

    [Fact]
    public async Task Handle_returns_error_for_invalid_role()
    {
        var result = await UpdateEmployeeHandler.Handle(
            new UpdateEmployeeCommand(
                Guid.NewGuid(),
                "Owner",
                null,
                8_000_000m,
                new DateOnly(2025, 1, 1),
                "Boss",
                null),
            _employees,
            _hierarchy,
            CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.Error>()
            .Which.Code.Should().Be("employee.role_invalid");
    }

    [Fact]
    public async Task Handle_updates_basic_info_and_persists()
    {
        var owner = NewOwner();
        _employees.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await UpdateEmployeeHandler.Handle(
            new UpdateEmployeeCommand(
                owner.Id.Value,
                "Owner Baru",
                null,
                owner.MonthlyWage.Amount,
                new DateOnly(2025, 1, 1),
                "Owner",
                null),
            _employees,
            _hierarchy,
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<EmployeeResult>.Success>().Subject;
        success.Value.FullName.Should().Be("Owner Baru");
        owner.DomainEvents.OfType<EmployeeBasicInfoChanged>().Should().HaveCount(1);
        await _employees.Received(1).UpdateAsync(owner, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_changes_salary_when_amount_or_date_differs()
    {
        var owner = NewOwner();
        _employees.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await UpdateEmployeeHandler.Handle(
            new UpdateEmployeeCommand(
                owner.Id.Value,
                owner.FullName,
                null,
                10_000_000m,
                new DateOnly(2025, 6, 1),
                "Owner",
                null),
            _employees,
            _hierarchy,
            CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.Success>();
        owner.DomainEvents.OfType<EmployeeSalaryChanged>().Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_returns_error_for_backdated_salary()
    {
        var owner = NewOwner();
        _employees.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await UpdateEmployeeHandler.Handle(
            new UpdateEmployeeCommand(
                owner.Id.Value,
                owner.FullName,
                null,
                10_000_000m,
                new DateOnly(2024, 12, 1),
                "Owner",
                null),
            _employees,
            _hierarchy,
            CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.Error>()
            .Which.Code.Should().Be("employee.salary_backdated");
        await _employees.DidNotReceive().UpdateAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_rejects_when_new_parent_chain_too_deep()
    {
        var manager = Employee.Create(
            "Manager",
            Nik.Create("3201234567890124"),
            Money.Idr(8_000_000m),
            new LocalDate(2025, 1, 1),
            EmployeeRole.Manager,
            parentId: EmployeeId.New());
        _employees.GetByIdAsync(manager.Id, Arg.Any<CancellationToken>()).Returns(manager);

        var newParentId = EmployeeId.New();
        _hierarchy.GetAncestorsAsync(newParentId, Arg.Any<CancellationToken>())
            .Returns(new[] { EmployeeId.New(), EmployeeId.New() });

        var result = await UpdateEmployeeHandler.Handle(
            new UpdateEmployeeCommand(
                manager.Id.Value,
                manager.FullName,
                null,
                manager.MonthlyWage.Amount,
                new DateOnly(2025, 1, 1),
                "Manager",
                newParentId.Value),
            _employees,
            _hierarchy,
            CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.Error>()
            .Which.Code.Should().Be("employee.depth_exceeded");
        await _employees.DidNotReceive().UpdateAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
    }
}
