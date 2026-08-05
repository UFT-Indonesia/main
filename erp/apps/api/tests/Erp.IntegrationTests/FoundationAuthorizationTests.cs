using System.Net;
using System.Net.Http.Json;
using Erp.Core.Aggregates.Employees;
using FluentAssertions;
using NodaTime;

namespace Erp.IntegrationTests;

/// <summary>
/// PR1 rules at the HTTP boundary: the role on a token comes from the employee record, and
/// termination closes the employee to new writes.
/// </summary>
public class FoundationAuthorizationTests : IntegrationTestBase
{
    public FoundationAuthorizationTests(ErpApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Token_carries_the_role_and_employee_from_the_employee_record()
    {
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu");
        var client = await CreateClientForAsync(manager);

        var me = await client.GetFromJsonAsync<LoginUser>("/api/auth/me");

        me.Should().NotBeNull();
        me!.Roles.Should().ContainSingle().Which.Should().Be(nameof(EmployeeRole.Manager));
        me.EmployeeId.Should().Be(manager.Id.Value);
    }

    [Fact]
    public async Task Changing_the_employee_role_changes_the_role_on_the_next_token()
    {
        var employee = await CreateEmployeeAsync(EmployeeRole.Staff, "Naik Pangkat");
        var username = $"user-{employee.Id.Value:N}";
        using (var _ = await CreateClientForAsync(employee)) { }

        await using (var db = Factory.CreateDbContext())
        {
            var stored = await db.Employees.FindAsync(employee.Id);
            stored!.ChangeRole(EmployeeRole.Manager);
            await db.SaveChangesAsync();
        }

        var client = await LoginAsync(username, "Passw0rd!");
        var me = await client.GetFromJsonAsync<LoginUser>("/api/auth/me");

        me!.Roles.Should().ContainSingle().Which.Should().Be(nameof(EmployeeRole.Manager));
    }

    [Fact]
    public async Task Staff_cannot_read_the_employees_list()
    {
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa");
        var client = await CreateClientForAsync(staff);

        var response = await client.GetAsync("/api/employees/");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_can_read_the_employees_list()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var client = await CreateClientForAsync(owner);

        var response = await client.GetAsync("/api/employees/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Terminated_employees_reject_new_manual_punches()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Sudah Keluar", owner.Id);
        await TerminateAsync(staff.Id);

        var client = await CreateClientForAsync(owner);
        var response = await client.PostAsJsonAsync("/api/attendance/manual-logs", new
        {
            employeeId = staff.Id.Value,
            punchedAtUtc = DateTimeOffset.UtcNow,
            punchType = "In",
            note = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("attendance.employee_terminated");
    }

    [Fact]
    public async Task Terminated_employees_reject_new_leave_requests()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Sudah Keluar", owner.Id);
        await TerminateAsync(staff.Id);

        var client = await CreateClientForAsync(owner);
        var response = await client.PostAsJsonAsync("/api/leave/", new
        {
            employeeId = staff.Id.Value,
            type = "Annual",
            startDate = "2026-09-01",
            endDate = "2026-09-04",
            reason = "cuti",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("leave.employee_terminated");
    }

    [Fact]
    public async Task A_manager_with_active_reports_cannot_be_terminated()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        await CreateEmployeeAsync(EmployeeRole.Staff, "Anak Buah", manager.Id);

        var client = await CreateClientForAsync(owner);
        var response = await client.DeleteAsync($"/api/employees/{manager.Id.Value}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("employee.has_active_reports");
        body.Should().Contain("Anak Buah");
    }

    [Fact]
    public async Task A_manager_whose_reports_were_reassigned_can_be_terminated()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Anak Buah", manager.Id);

        await using (var db = Factory.CreateDbContext())
        {
            var stored = await db.Employees.FindAsync(staff.Id);
            stored!.AssignParent(owner.Id, []);
            await db.SaveChangesAsync();
        }

        var client = await CreateClientForAsync(owner);
        var response = await client.DeleteAsync($"/api/employees/{manager.Id.Value}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task TerminateAsync(Erp.SharedKernel.Identity.EmployeeId employeeId)
    {
        await using var db = Factory.CreateDbContext();
        var employee = await db.Employees.FindAsync(employeeId);
        employee!.Terminate(new LocalDate(2026, 8, 1));
        await db.SaveChangesAsync();
    }
}
