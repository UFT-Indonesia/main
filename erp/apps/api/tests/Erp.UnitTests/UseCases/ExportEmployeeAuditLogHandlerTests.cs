using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.ExportEmployeeAuditLog;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class ExportEmployeeAuditLogHandlerTests
{
    private readonly IReadRepository<EmployeeAuditLog> _auditLogs = Substitute.For<IReadRepository<EmployeeAuditLog>>();
    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();

    [Fact]
    public async Task Handle_returns_error_when_over_row_cap()
    {
        _auditLogs.CountAsync(Arg.Any<ISpecification<EmployeeAuditLog>>(), Arg.Any<CancellationToken>())
            .Returns(ExportEmployeeAuditLogHandler.MaxRows + 1);

        var result = await ExportEmployeeAuditLogHandler.Handle(
            new ExportEmployeeAuditLogQuery(null, null, null, null),
            _auditLogs,
            _employees,
            CancellationToken.None);

        result.Should().BeOfType<Result<ExportEmployeeAuditLogResult>.Error>()
            .Which.Code.Should().Be("employee_audit_log.export_too_many");
        await _auditLogs.DidNotReceive().ListAsync(Arg.Any<ISpecification<EmployeeAuditLog>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_rows_when_within_cap()
    {
        var log = EmployeeAuditLog.Create(
            Erp.SharedKernel.Identity.EmployeeId.New(), "employee.created",
            Instant.FromUtc(2026, 1, 2, 0, 0), null, "{\"fullName\":\"Staff Satu\"}");

        _auditLogs.CountAsync(Arg.Any<ISpecification<EmployeeAuditLog>>(), Arg.Any<CancellationToken>())
            .Returns(1);
        _auditLogs.ListAsync(Arg.Any<ISpecification<EmployeeAuditLog>>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmployeeAuditLog> { log });
        _employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee>());

        var result = await ExportEmployeeAuditLogHandler.Handle(
            new ExportEmployeeAuditLogQuery(null, null, null, null),
            _auditLogs,
            _employees,
            CancellationToken.None);

        var success = result.Should().BeOfType<Result<ExportEmployeeAuditLogResult>.Success>().Subject;
        success.Value.Rows.Should().ContainSingle();
        success.Value.Rows[0].EventType.Should().Be("employee.created");
        success.Value.Rows[0].EmployeeFullName.Should().Be("—");
    }
}
