using Ardalis.Specification;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.ListEmployees;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class ListEmployeesHandlerTests
{
    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();

    private ListEmployeesHandler Sut() => new(_employees);

    [Fact]
    public async Task Handle_returns_paged_results()
    {
        var owner = Employee.Create(
            "Owner",
            Nik.Create("3201234567890123"),
            Money.Idr(8_000_000m),
            new LocalDate(2025, 1, 1),
            EmployeeRole.Owner);

        _employees.CountAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(1);
        _employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee> { owner });

        var result = await Sut().Handle(
            new ListEmployeesQuery(Page: 1, PageSize: 20, Search: null, Role: null, Status: null),
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListEmployeesResult>.Success>().Subject;
        success.Value.Items.Should().HaveCount(1);
        success.Value.TotalCount.Should().Be(1);
        success.Value.Page.Should().Be(1);
        success.Value.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_clamps_page_size_to_max()
    {
        _employees.CountAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee>());

        var result = await Sut().Handle(
            new ListEmployeesQuery(Page: 1, PageSize: 1000, Search: null, Role: null, Status: null),
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListEmployeesResult>.Success>().Subject;
        success.Value.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Handle_defaults_page_size_when_zero()
    {
        _employees.CountAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee>());

        var result = await Sut().Handle(
            new ListEmployeesQuery(Page: 0, PageSize: 0, Search: null, Role: null, Status: null),
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListEmployeesResult>.Success>().Subject;
        success.Value.Page.Should().Be(1);
        success.Value.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_returns_error_for_invalid_role_filter()
    {
        var result = await Sut().Handle(
            new ListEmployeesQuery(1, 20, null, "Boss", null),
            CancellationToken.None);

        result.Should().BeOfType<Result<ListEmployeesResult>.Error>()
            .Which.Code.Should().Be("employee.role_invalid");
    }

    [Fact]
    public async Task Handle_returns_error_for_invalid_status_filter()
    {
        var result = await Sut().Handle(
            new ListEmployeesQuery(1, 20, null, null, "Vacation"),
            CancellationToken.None);

        result.Should().BeOfType<Result<ListEmployeesResult>.Error>()
            .Which.Code.Should().Be("employee.status_invalid");
    }
}
