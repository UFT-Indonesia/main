using System.Text.Json;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.DomainEventHandlers;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using FluentAssertions;
using NodaTime;
using NSubstitute;
using Wolverine;

namespace Erp.UnitTests.UseCases;

public class EmployeeAuditLogHandlersTests
{
    private static readonly Guid ActorUserId = Guid.NewGuid();

    private readonly IRepository<EmployeeAuditLog> _auditLogs = Substitute.For<IRepository<EmployeeAuditLog>>();
    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();
    private readonly Envelope _envelope = EnvelopeWithActor(ActorUserId, "Owner Utama");

    private EmployeeAuditLog? Captured;

    public EmployeeAuditLogHandlersTests()
    {
        _auditLogs.AddAsync(Arg.Do<EmployeeAuditLog>(log => Captured = log), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<EmployeeAuditLog>());
    }

    private static Envelope EnvelopeWithActor(Guid? userId, string? name)
    {
        var envelope = new Envelope();
        if (userId is { } id)
        {
            envelope.Headers[EmployeeAuditHeaders.ActorUserId] = id.ToString();
        }

        if (name is not null)
        {
            envelope.Headers[EmployeeAuditHeaders.ActorName] = name;
        }

        return envelope;
    }

    [Fact]
    public async Task EmployeeCreatedHandler_writes_audit_row_with_initial_snapshot()
    {
        var employeeId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var parent = Employee.Create(
            "Manager Satu", Nik.Create("3201234567890124"), Money.Idr(10_000_000m),
            new LocalDate(2024, 1, 1), EmployeeRole.Owner, id: new EmployeeId(parentId));
        _employees.GetByIdAsync(new EmployeeId(parentId), Arg.Any<CancellationToken>()).Returns(parent);

        var message = new EmployeeCreated(
            employeeId, "Staff Satu", "3201234567890123", null,
            EmployeeRole.Staff, parentId, Money.Idr(5_000_000m), new LocalDate(2026, 1, 1));

        await EmployeeCreatedHandler.Handle(message, _auditLogs, _employees, _envelope, CancellationToken.None);

        Captured.Should().NotBeNull();
        Captured!.EmployeeId.Should().Be(new EmployeeId(employeeId));
        Captured.EventType.Should().Be("employee.created");
        Captured.OldValueJson.Should().BeNull();
        Captured.ActorUserId.Should().Be(ActorUserId);
        Captured.ActorName.Should().Be("Owner Utama");

        using var doc = JsonDocument.Parse(Captured.NewValueJson!);
        doc.RootElement.GetProperty("fullName").GetString().Should().Be("Staff Satu");
        doc.RootElement.GetProperty("parentName").GetString().Should().Be("Manager Satu");
    }

    [Fact]
    public async Task EmployeeBasicInfoChangedHandler_writes_old_and_new_values()
    {
        var employeeId = Guid.NewGuid();
        var message = new EmployeeBasicInfoChanged(employeeId, "Old Name", "New Name", null, "12.345.678.9-012.000");

        await EmployeeBasicInfoChangedHandler.Handle(message, _auditLogs, _envelope, CancellationToken.None);

        Captured.Should().NotBeNull();
        Captured!.EventType.Should().Be("employee.basic_info_changed");
        Captured.ActorName.Should().Be("Owner Utama");
        using var oldDoc = JsonDocument.Parse(Captured.OldValueJson!);
        oldDoc.RootElement.GetProperty("fullName").GetString().Should().Be("Old Name");
        using var newDoc = JsonDocument.Parse(Captured.NewValueJson!);
        newDoc.RootElement.GetProperty("fullName").GetString().Should().Be("New Name");
    }

    [Fact]
    public async Task EmployeeSalaryChangedHandler_writes_old_and_new_wage()
    {
        var employeeId = Guid.NewGuid();
        var message = new EmployeeSalaryChanged(
            employeeId, Money.Idr(5_000_000m), new LocalDate(2025, 1, 1),
            Money.Idr(5_500_000m), new LocalDate(2026, 1, 1));

        await EmployeeSalaryChangedHandler.Handle(message, _auditLogs, _envelope, CancellationToken.None);

        Captured.Should().NotBeNull();
        Captured!.EventType.Should().Be("employee.salary_changed");
        using var oldDoc = JsonDocument.Parse(Captured.OldValueJson!);
        oldDoc.RootElement.GetProperty("monthlyWageAmount").GetDecimal().Should().Be(5_000_000m);
        using var newDoc = JsonDocument.Parse(Captured.NewValueJson!);
        newDoc.RootElement.GetProperty("monthlyWageAmount").GetDecimal().Should().Be(5_500_000m);
    }

    [Fact]
    public async Task EmployeeParentChangedHandler_snapshots_old_and_new_parent_names()
    {
        var employeeId = Guid.NewGuid();
        var oldParentId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();

        var oldParent = Employee.Create(
            "Old Manager", Nik.Create("3201234567890125"), Money.Idr(10_000_000m),
            new LocalDate(2024, 1, 1), EmployeeRole.Owner, id: new EmployeeId(oldParentId));
        var newParent = Employee.Create(
            "New Manager", Nik.Create("3201234567890126"), Money.Idr(10_000_000m),
            new LocalDate(2024, 1, 1), EmployeeRole.Owner, id: new EmployeeId(newParentId));

        _employees.GetByIdAsync(new EmployeeId(oldParentId), Arg.Any<CancellationToken>()).Returns(oldParent);
        _employees.GetByIdAsync(new EmployeeId(newParentId), Arg.Any<CancellationToken>()).Returns(newParent);

        var message = new EmployeeParentChanged(employeeId, oldParentId, newParentId);

        await EmployeeParentChangedHandler.Handle(message, _auditLogs, _employees, _envelope, CancellationToken.None);

        Captured.Should().NotBeNull();
        Captured!.EventType.Should().Be("employee.parent_changed");
        using var oldDoc = JsonDocument.Parse(Captured.OldValueJson!);
        oldDoc.RootElement.GetProperty("parentName").GetString().Should().Be("Old Manager");
        using var newDoc = JsonDocument.Parse(Captured.NewValueJson!);
        newDoc.RootElement.GetProperty("parentName").GetString().Should().Be("New Manager");
    }

    [Fact]
    public async Task EmployeeParentChangedHandler_handles_null_old_parent()
    {
        var employeeId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();
        var newParent = Employee.Create(
            "New Manager", Nik.Create("3201234567890127"), Money.Idr(10_000_000m),
            new LocalDate(2024, 1, 1), EmployeeRole.Owner, id: new EmployeeId(newParentId));
        _employees.GetByIdAsync(new EmployeeId(newParentId), Arg.Any<CancellationToken>()).Returns(newParent);

        var message = new EmployeeParentChanged(employeeId, null, newParentId);

        await EmployeeParentChangedHandler.Handle(message, _auditLogs, _employees, _envelope, CancellationToken.None);

        using var oldDoc = JsonDocument.Parse(Captured!.OldValueJson!);
        oldDoc.RootElement.GetProperty("parentId").ValueKind.Should().Be(JsonValueKind.Null);
        oldDoc.RootElement.GetProperty("parentName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task EmployeeRoleChangedHandler_audits_the_transition_and_revokes_tokens()
    {
        var refreshTokens = Substitute.For<IRefreshTokenService>();
        var employeeId = Guid.NewGuid();
        var message = new EmployeeRoleChanged(employeeId, EmployeeRole.Staff, EmployeeRole.Manager);

        await EmployeeRoleChangedHandler.Handle(
            message, refreshTokens, _auditLogs, _envelope, CancellationToken.None);

        await refreshTokens.Received(1).RevokeAllForEmployeeAsync(
            employeeId, "employee_role_changed", Arg.Any<CancellationToken>());

        Captured.Should().NotBeNull();
        Captured!.EventType.Should().Be("employee.role_changed");
        using var oldDoc = JsonDocument.Parse(Captured.OldValueJson!);
        oldDoc.RootElement.GetProperty("role").GetString().Should().Be("Staff");
        using var newDoc = JsonDocument.Parse(Captured.NewValueJson!);
        newDoc.RootElement.GetProperty("role").GetString().Should().Be("Manager");
    }

    [Fact]
    public async Task EmployeeTerminatedHandler_records_the_termination_date()
    {
        var message = new EmployeeTerminated(Guid.NewGuid(), new LocalDate(2026, 3, 31));

        await EmployeeTerminatedHandler.Handle(message, _auditLogs, _envelope, CancellationToken.None);

        Captured.Should().NotBeNull();
        Captured!.EventType.Should().Be("employee.terminated");
        Captured.OldValueJson.Should().BeNull();
        using var newDoc = JsonDocument.Parse(Captured.NewValueJson!);
        newDoc.RootElement.GetProperty("terminationDate").GetString().Should().Be("2026-03-31");
    }

    [Fact]
    public async Task Handler_still_writes_the_row_when_no_actor_is_stamped()
    {
        var message = new EmployeeSalaryChanged(
            Guid.NewGuid(), Money.Idr(5_000_000m), new LocalDate(2025, 1, 1),
            Money.Idr(5_500_000m), new LocalDate(2026, 1, 1));

        await EmployeeSalaryChangedHandler.Handle(
            message, _auditLogs, EnvelopeWithActor(null, null), CancellationToken.None);

        Captured.Should().NotBeNull();
        Captured!.ActorUserId.Should().BeNull();
        Captured.ActorName.Should().BeNull();
    }
}
