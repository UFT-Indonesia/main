using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.CreateEmployee;
using FluentAssertions;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class CreateEmployeeHandlerTests
{
    private readonly IRepository<Employee> _employees = Substitute.For<IRepository<Employee>>();

    private CreateEmployeeHandler Sut() => new(_employees);

    [Fact]
    public async Task Handle_creates_owner_employee()
    {
        var command = new CreateEmployeeCommand(
            "Owner Satu",
            "3201234567890123",
            Npwp: null,
            MonthlyWageAmount: 8_000_000m,
            EffectiveSalaryFrom: new DateOnly(2025, 1, 1),
            Role: "Owner",
            ParentId: null);

        var result = await Sut().Handle(command, CancellationToken.None);

        var success = result.Should().BeOfType<Result<EmployeeResult>.Success>().Subject;
        success.Value.FullName.Should().Be("Owner Satu");
        success.Value.Role.Should().Be("Owner");
        success.Value.Status.Should().Be("Active");
        await _employees.Received(1).AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_error_for_invalid_role()
    {
        var command = new CreateEmployeeCommand(
            "Bos",
            "3201234567890123",
            null,
            8_000_000m,
            new DateOnly(2025, 1, 1),
            "Boss",
            null);

        var result = await Sut().Handle(command, CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.Error>()
            .Which.Code.Should().Be("employee.role_invalid");
        await _employees.DidNotReceive().AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_error_for_invalid_nik()
    {
        var command = new CreateEmployeeCommand(
            "Owner",
            "123",
            null,
            8_000_000m,
            new DateOnly(2025, 1, 1),
            "Owner",
            null);

        var result = await Sut().Handle(command, CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.Error>()
            .Which.Code.Should().Be("nik.length");
    }

    [Fact]
    public async Task Handle_returns_error_for_owner_with_parent()
    {
        var command = new CreateEmployeeCommand(
            "Owner",
            "3201234567890123",
            null,
            8_000_000m,
            new DateOnly(2025, 1, 1),
            "Owner",
            Guid.NewGuid());

        var result = await Sut().Handle(command, CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.Error>()
            .Which.Code.Should().Be("employee.owner_no_parent");
    }

    [Fact]
    public async Task Handle_returns_error_for_non_owner_without_parent()
    {
        var command = new CreateEmployeeCommand(
            "Manager",
            "3201234567890123",
            null,
            8_000_000m,
            new DateOnly(2025, 1, 1),
            "Manager",
            null);

        var result = await Sut().Handle(command, CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.Error>()
            .Which.Code.Should().Be("employee.parent_required");
    }

    [Fact]
    public async Task Handle_accepts_npwp_when_provided()
    {
        var command = new CreateEmployeeCommand(
            "Owner",
            "3201234567890123",
            "12.345.678.9-012.345",
            8_000_000m,
            new DateOnly(2025, 1, 1),
            "owner",
            null);

        var result = await Sut().Handle(command, CancellationToken.None);

        var success = result.Should().BeOfType<Result<EmployeeResult>.Success>().Subject;
        success.Value.Npwp.Should().NotBeNullOrWhiteSpace();
    }
}
