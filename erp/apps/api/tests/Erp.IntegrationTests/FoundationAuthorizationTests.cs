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

    private sealed record EmployeeItem(
        Guid Id,
        string FullName,
        string Role,
        string Status,
        string? Nik,
        string? Npwp,
        decimal? MonthlyWageAmount);

    private sealed record EmployeeList(EmployeeItem[] Items, int TotalCount);

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
    public async Task Staff_read_the_directory_as_names_without_personal_details()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);
        var client = await CreateClientForAsync(staff);

        var list = await client.GetFromJsonAsync<EmployeeList>("/api/employees/");

        // Open so pickers can name people...
        list!.Items.Select(item => item.FullName)
            .Should().Contain(["Owner Utama", "Staff Biasa"]);

        // ...but a colleague is a name, not a national ID or a salary.
        var colleague = list.Items.Single(item => item.Id == owner.Id.Value);
        colleague.Nik.Should().BeNull("a national ID is not directory data");
        colleague.Npwp.Should().BeNull();
        colleague.MonthlyWageAmount.Should().BeNull("pay is Owner-only");
        colleague.FullName.Should().Be("Owner Utama");
        colleague.Role.Should().Be("Owner");

        // Their own record is still their own to read.
        list.Items.Single(item => item.Id == staff.Id.Value)
            .Nik.Should().NotBeNull("an employee may read their own record");
    }

    [Fact]
    public async Task Staff_still_cannot_create_update_or_delete_an_employee()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);
        var client = await CreateClientForAsync(staff);

        var create = await client.PostAsJsonAsync("/api/employees/", new
        {
            fullName = "Orang Baru",
            nik = "3204010101900001",
            monthlyWageAmount = 5_000_000m,
            effectiveSalaryFrom = "2026-01-01",
            role = "Staff",
            parentId = owner.Id.Value,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var delete = await client.DeleteAsync($"/api/employees/{staff.Id.Value}");
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_manager_may_only_edit_their_own_direct_staff()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var outsider = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Lain", owner.Id);
        var ownStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Anak Buah", manager.Id);
        var foreignStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Asing", outsider.Id);

        var client = await CreateClientForAsync(manager);

        var own = await client.PutAsJsonAsync($"/api/employees/{ownStaff.Id.Value}", new
        {
            fullName = "Anak Buah Baru",
            role = "Staff",
            parentId = manager.Id.Value,
        });
        own.StatusCode.Should().Be(HttpStatusCode.OK, "their own direct report");

        var foreign = await client.PutAsJsonAsync($"/api/employees/{foreignStaff.Id.Value}", new
        {
            fullName = "Diubah Diam-diam",
            role = "Staff",
            parentId = outsider.Id.Value,
        });
        foreign.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "another manager's line is not theirs to edit");
    }

    [Fact]
    public async Task A_manager_cannot_edit_staff_who_have_no_manager_assigned()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        // Placed directly under the owner, so no manager owns this person yet.
        var unassigned = await CreateEmployeeAsync(EmployeeRole.Staff, "Belum Ditempatkan", owner.Id);

        var client = await CreateClientForAsync(manager);
        var response = await client.PutAsJsonAsync($"/api/employees/{unassigned.Id.Value}", new
        {
            fullName = "Diubah",
            role = "Staff",
            parentId = owner.Id.Value,
        });

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "the owner assigns them before a manager may act");
    }

    [Fact]
    public async Task A_manager_reads_details_for_their_own_staff_but_not_another_line()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var outsider = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Lain", owner.Id);
        var ownStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Anak Buah", manager.Id);
        var foreignStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Asing", outsider.Id);

        var client = await CreateClientForAsync(manager);
        var list = await client.GetFromJsonAsync<EmployeeList>("/api/employees/");

        list!.Items.Single(item => item.Id == ownStaff.Id.Value)
            .Nik.Should().NotBeNull("their own direct staff");
        list.Items.Single(item => item.Id == foreignStaff.Id.Value)
            .Nik.Should().BeNull("another manager's line");
        list.Items.Single(item => item.Id == owner.Id.Value)
            .Nik.Should().BeNull("nobody below the owner reads the owner's record");
        list.Items.Single(item => item.Id == manager.Id.Value)
            .Nik.Should().NotBeNull("their own record");
        list.Items.Should().OnlyContain(item => item.MonthlyWageAmount == null, "pay is Owner-only");
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
