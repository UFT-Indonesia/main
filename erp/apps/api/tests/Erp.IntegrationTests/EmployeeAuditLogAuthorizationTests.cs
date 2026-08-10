using System.Net;
using System.Net.Http.Json;
using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using NodaTime;

namespace Erp.IntegrationTests;

/// <summary>Audit log is Owner-only end to end — Manager/Staff must be refused even though
/// they can both read the Employees list itself.</summary>
public class EmployeeAuditLogAuthorizationTests : IntegrationTestBase
{
    public EmployeeAuditLogAuthorizationTests(ErpApiFactory factory) : base(factory) { }

    private sealed record AuditLogEntry(Guid Id, Guid EmployeeId, string EmployeeFullName, string EventType);

    private sealed record AuditLogList(AuditLogEntry[] Items, int TotalCount);

    private async Task SeedAuditRowAsync(EmployeeId employeeId)
    {
        await using var db = Factory.CreateDbContext();
        db.EmployeeAuditLogs.Add(EmployeeAuditLog.Create(
            employeeId, "employee.basic_info_changed", SystemClock.Instance.GetCurrentInstant(),
            "{\"fullName\":\"Old Name\"}", "{\"fullName\":\"New Name\"}"));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Owner_can_list_the_audit_log()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        await SeedAuditRowAsync(owner.Id);

        var client = await CreateClientForAsync(owner);
        var response = await client.GetAsync("/api/employees/audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<AuditLogList>();
        list!.TotalCount.Should().Be(1);
        list.Items[0].EmployeeFullName.Should().Be("Owner Utama");
    }

    [Fact]
    public async Task Manager_cannot_list_the_audit_log()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);

        var client = await CreateClientForAsync(manager);
        var response = await client.GetAsync("/api/employees/audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Staff_cannot_export_the_audit_log()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);

        var client = await CreateClientForAsync(staff);
        var response = await client.GetAsync("/api/employees/audit-log/export");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_can_export_the_audit_log_as_csv()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        await SeedAuditRowAsync(owner.Id);

        var client = await CreateClientForAsync(owner);
        var response = await client.GetAsync("/api/employees/audit-log/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("Owner Utama");
    }

    [Fact]
    public async Task Owner_can_filter_by_employee()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        await SeedAuditRowAsync(owner.Id);
        await SeedAuditRowAsync(manager.Id);

        var client = await CreateClientForAsync(owner);
        var response = await client.GetAsync($"/api/employees/audit-log?employeeId={manager.Id.Value}");

        var list = await response.Content.ReadFromJsonAsync<AuditLogList>();
        list!.TotalCount.Should().Be(1);
        list.Items[0].EmployeeId.Should().Be(manager.Id.Value);
    }
}
