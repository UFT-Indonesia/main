using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.GetEmployeeById;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class GetEmployeeByIdHandlerTests
{
    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();

    private GetEmployeeByIdHandler Sut() => new(_employees);

    [Fact]
    public async Task Handle_returns_success_when_employee_exists()
    {
        var employee = Employee.Create(
            "Owner",
            Nik.Create("3201234567890123"),
            Money.Idr(8_000_000m),
            new LocalDate(2025, 1, 1),
            EmployeeRole.Owner);
        var typedId = employee.Id;

        _employees.GetByIdAsync(typedId, Arg.Any<CancellationToken>()).Returns(employee);

        var result = await Sut().Handle(
            new GetEmployeeByIdQuery(typedId.Value),
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<EmployeeResult>.Success>().Subject;
        success.Value.Id.Should().Be(typedId.Value);
        success.Value.FullName.Should().Be("Owner");
    }

    [Fact]
    public async Task Handle_returns_not_found_when_employee_missing()
    {
        _employees.GetByIdAsync(Arg.Any<EmployeeId>(), Arg.Any<CancellationToken>())
            .Returns((Employee?)null);

        var result = await Sut().Handle(
            new GetEmployeeByIdQuery(Guid.NewGuid()),
            CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.NotFound>();
    }
}
