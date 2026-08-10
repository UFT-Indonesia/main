using Ardalis.Specification;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.ListEmployeeAuditLog;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class ListEmployeeAuditLogHandlerTests
{
    private readonly IReadRepository<EmployeeAuditLog> _auditLogs = Substitute.For<IReadRepository<EmployeeAuditLog>>();
    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();

    [Fact]
    public async Task Handle_returns_paged_results_with_resolved_employee_names()
    {
        var employee = Employee.Create(
            "Staff Satu", Nik.Create("3201234567890123"), Money.Idr(5_000_000m),
            new LocalDate(2026, 1, 1), EmployeeRole.Staff, parentId: EmployeeId.New());

        var log = EmployeeAuditLog.Create(
            employee.Id, "employee.salary_changed", Instant.FromUtc(2026, 1, 2, 0, 0), "{}", "{}");

        _auditLogs.CountAsync(Arg.Any<ISpecification<EmployeeAuditLog>>(), Arg.Any<CancellationToken>())
            .Returns(1);
        _auditLogs.ListAsync(Arg.Any<ISpecification<EmployeeAuditLog>>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmployeeAuditLog> { log });
        _employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee> { employee });

        var result = await ListEmployeeAuditLogHandler.Handle(
            new ListEmployeeAuditLogQuery(1, 20, null, null, null, null),
            _auditLogs,
            _employees,
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListEmployeeAuditLogResult>.Success>().Subject;
        success.Value.TotalCount.Should().Be(1);
        success.Value.Items.Should().ContainSingle();
        success.Value.Items[0].EmployeeFullName.Should().Be("Staff Satu");
        success.Value.Items[0].EventType.Should().Be("employee.salary_changed");
    }

    [Fact]
    public async Task Handle_defaults_and_clamps_paging()
    {
        _auditLogs.CountAsync(Arg.Any<ISpecification<EmployeeAuditLog>>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _auditLogs.ListAsync(Arg.Any<ISpecification<EmployeeAuditLog>>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmployeeAuditLog>());
        _employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee>());

        var result = await ListEmployeeAuditLogHandler.Handle(
            new ListEmployeeAuditLogQuery(0, 1000, null, null, null, null),
            _auditLogs,
            _employees,
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListEmployeeAuditLogResult>.Success>().Subject;
        success.Value.Page.Should().Be(1);
        success.Value.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Handle_falls_back_to_placeholder_when_employee_not_found()
    {
        var missingEmployeeId = EmployeeId.New();
        var log = EmployeeAuditLog.Create(
            missingEmployeeId, "employee.created", Instant.FromUtc(2026, 1, 2, 0, 0), null, "{}");

        _auditLogs.CountAsync(Arg.Any<ISpecification<EmployeeAuditLog>>(), Arg.Any<CancellationToken>())
            .Returns(1);
        _auditLogs.ListAsync(Arg.Any<ISpecification<EmployeeAuditLog>>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmployeeAuditLog> { log });
        _employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee>());

        var result = await ListEmployeeAuditLogHandler.Handle(
            new ListEmployeeAuditLogQuery(1, 20, null, null, null, null),
            _auditLogs,
            _employees,
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListEmployeeAuditLogResult>.Success>().Subject;
        success.Value.Items[0].EmployeeFullName.Should().Be("—");
    }
}
