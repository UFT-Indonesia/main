using Ardalis.Specification;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;
using Erp.UseCases.Employees.ListEmployees;
using FluentAssertions;
using NodaTime;
using NSubstitute;

using static Erp.UnitTests.UseCases.FilterRows;

namespace Erp.UnitTests.UseCases;

public class ListEmployeesHandlerTests
{
    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();

    private static readonly Caller Owner =
        new(Guid.NewGuid(), EmployeeRole.Owner, new EmployeeId(Guid.NewGuid()), "Owner");

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

        var result = await ListEmployeesHandler.Handle(
            new ListEmployeesQuery(Page: 1, PageSize: 20, Filters: None, Caller: Owner),
            _employees,
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

        var result = await ListEmployeesHandler.Handle(
            new ListEmployeesQuery(Page: 1, PageSize: 1000, Filters: None, Caller: Owner),
            _employees,
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

        var result = await ListEmployeesHandler.Handle(
            new ListEmployeesQuery(Page: 0, PageSize: 0, Filters: None, Caller: Owner),
            _employees,
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListEmployeesResult>.Success>().Subject;
        success.Value.Page.Should().Be(1);
        success.Value.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_rejects_an_unknown_filter_field()
    {
        var result = await ListEmployeesHandler.Handle(
            new ListEmployeesQuery(1, 20, Rows(Row("password", FilterOps.Is, "\"x\"")), Owner),
            _employees,
            CancellationToken.None);

        result.Should().BeOfType<Result<ListEmployeesResult>.Error>()
            .Which.Code.Should().Be(FilterErrors.UnknownField);
    }

    [Fact]
    public async Task Handle_rejects_an_operator_the_datatype_does_not_allow()
    {
        var result = await ListEmployeesHandler.Handle(
            new ListEmployeesQuery(1, 20, Rows(Row("role", FilterOps.Contains, "\"Own\"")), Owner),
            _employees,
            CancellationToken.None);

        result.Should().BeOfType<Result<ListEmployeesResult>.Error>()
            .Which.Code.Should().Be(FilterErrors.InvalidOperator);
    }

    [Fact]
    public async Task Handle_rejects_a_value_the_domain_will_not_accept()
    {
        var result = await ListEmployeesHandler.Handle(
            new ListEmployeesQuery(1, 20, Rows(Row("nik", FilterOps.Is, "\"123\"")), Owner),
            _employees,
            CancellationToken.None);

        result.Should().BeOfType<Result<ListEmployeesResult>.Error>()
            .Which.Code.Should().Be(FilterErrors.InvalidValue);
    }

    /// <summary>
    /// The directory is readable by every employee but NIK, NPWP and pay are redacted per row.
    /// A filter on one of those would return the answer as a row count, so it must be refused
    /// outright rather than applied and redacted afterwards.
    /// </summary>
    [Theory]
    [InlineData("nik", FilterOps.Is, "\"3201234567890123\"")]
    [InlineData("npwp", FilterOps.Is, "\"091234567890123\"")]
    [InlineData("monthlyWage", FilterOps.GreaterThan, "1000000")]
    [InlineData("terminationDate", FilterOps.Before, "\"2024-01-01\"")]
    public async Task Handle_refuses_a_redacted_field_for_a_caller_without_standing(
        string field, string op, string valueJson)
    {
        foreach (var role in new[] { EmployeeRole.Staff, EmployeeRole.Manager })
        {
            var caller = new Caller(Guid.NewGuid(), role, new EmployeeId(Guid.NewGuid()), role.ToString());

            var result = await ListEmployeesHandler.Handle(
                new ListEmployeesQuery(1, 20, Rows(Row(field, op, valueJson)), caller),
                _employees,
                CancellationToken.None);

            result.Should().BeOfType<Result<ListEmployeesResult>.Error>(
                    $"{role} must not be able to filter by {field}")
                .Which.Code.Should().Be(FilterErrors.FieldForbidden);
        }
    }
}
