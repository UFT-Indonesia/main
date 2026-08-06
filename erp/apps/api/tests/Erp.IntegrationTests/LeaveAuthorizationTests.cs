using System.Net;
using System.Net.Http.Json;
using Erp.Core.Aggregates.Employees;
using FluentAssertions;

namespace Erp.IntegrationTests;

/// <summary>
/// PR2 rules end to end: who may file, who may decide, and — the part unit tests cannot
/// reach — what the list query actually returns for each caller.
/// </summary>
public class LeaveAuthorizationTests : IntegrationTestBase
{
    public LeaveAuthorizationTests(ErpApiFactory factory) : base(factory) { }

    private sealed record LeaveItem(
        Guid Id,
        Guid EmployeeId,
        string EmployeeFullName,
        string Status,
        bool CanDecide,
        bool CanCancel);

    private sealed record LeaveList(LeaveItem[] Items, int TotalCount);

    private static object NewRequestFor(Guid employeeId) => new
    {
        employeeId,
        type = "Annual",
        startDate = "2026-09-01",
        endDate = "2026-09-04",
        reason = "cuti",
    };

    [Fact]
    public async Task Staff_can_file_their_own_leave()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);

        var client = await CreateClientForAsync(staff);
        var response = await client.PostAsJsonAsync("/api/leave/", NewRequestFor(staff.Id.Value));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Staff_cannot_file_for_a_colleague()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);
        var colleague = await CreateEmployeeAsync(EmployeeRole.Staff, "Rekan Kerja", manager.Id);

        var client = await CreateClientForAsync(staff);
        var response = await client.PostAsJsonAsync("/api/leave/", NewRequestFor(colleague.Id.Value));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_manager_cannot_file_on_behalf_of_the_owner()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);

        var client = await CreateClientForAsync(manager);
        var response = await client.PostAsJsonAsync("/api/leave/", NewRequestFor(owner.Id.Value));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_leave_is_approved_the_moment_it_is_filed()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");

        var client = await CreateClientForAsync(owner);
        var response = await client.PostAsJsonAsync("/api/leave/", NewRequestFor(owner.Id.Value));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<LeaveItem>();
        created!.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task A_manager_who_filed_for_their_staff_cannot_also_approve_it()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);

        var managerClient = await CreateClientForAsync(manager);
        var created = await managerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(staff.Id.Value));
        var request = await created.Content.ReadFromJsonAsync<LeaveItem>();

        var selfApprove = await managerClient.PostAsJsonAsync($"/api/leave/{request!.Id}/approve", new { });
        selfApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ownerClient = await CreateClientForAsync(owner);
        var byOwner = await ownerClient.PostAsJsonAsync($"/api/leave/{request.Id}/approve", new { });
        byOwner.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_manager_approves_leave_their_staff_filed_themselves()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);

        var staffClient = await CreateClientForAsync(staff);
        var created = await staffClient.PostAsJsonAsync("/api/leave/", NewRequestFor(staff.Id.Value));
        var request = await created.Content.ReadFromJsonAsync<LeaveItem>();

        var managerClient = await CreateClientForAsync(manager);
        var approve = await managerClient.PostAsJsonAsync($"/api/leave/{request!.Id}/approve", new { });

        approve.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unrelated_manager_cannot_approve()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var outsider = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Lain", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);

        var staffClient = await CreateClientForAsync(staff);
        var created = await staffClient.PostAsJsonAsync("/api/leave/", NewRequestFor(staff.Id.Value));
        var request = await created.Content.ReadFromJsonAsync<LeaveItem>();

        var outsiderClient = await CreateClientForAsync(outsider);
        var approve = await outsiderClient.PostAsJsonAsync($"/api/leave/{request!.Id}/approve", new { });

        approve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_list_shows_staff_only_their_own_requests()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);
        var colleague = await CreateEmployeeAsync(EmployeeRole.Staff, "Rekan Kerja", manager.Id);

        var ownerClient = await CreateClientForAsync(owner);
        await ownerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(staff.Id.Value));
        await ownerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(colleague.Id.Value));

        var staffClient = await CreateClientForAsync(staff);
        var list = await staffClient.GetFromJsonAsync<LeaveList>("/api/leave/?status=");

        list!.Items.Should().OnlyContain(item => item.EmployeeId == staff.Id.Value);
        list.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task The_list_shows_a_manager_their_own_staff_but_not_other_lines()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var outsider = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Lain", owner.Id);
        var ownStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Anak Buah", manager.Id);
        var foreignStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Asing", outsider.Id);

        var ownerClient = await CreateClientForAsync(owner);
        await ownerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(ownStaff.Id.Value));
        await ownerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(foreignStaff.Id.Value));
        await ownerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(owner.Id.Value));

        var managerClient = await CreateClientForAsync(manager);
        var list = await managerClient.GetFromJsonAsync<LeaveList>("/api/leave/?status=");

        list!.Items.Select(item => item.EmployeeId).Should().BeEquivalentTo([ownStaff.Id.Value]);
        list.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Permission_flags_match_what_the_caller_may_actually_do()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);

        var staffClient = await CreateClientForAsync(staff);
        await staffClient.PostAsJsonAsync("/api/leave/", NewRequestFor(staff.Id.Value));

        var ownList = await staffClient.GetFromJsonAsync<LeaveList>("/api/leave/?status=");
        var own = ownList!.Items.Single();
        own.CanDecide.Should().BeFalse("staff never approve their own leave");
        own.CanCancel.Should().BeTrue("the subject may always withdraw their own request");

        var managerClient = await CreateClientForAsync(manager);
        var managerList = await managerClient.GetFromJsonAsync<LeaveList>("/api/leave/?status=");
        var asManager = managerList!.Items.Single(item => item.EmployeeId == staff.Id.Value);
        asManager.CanDecide.Should().BeTrue("the request was filed by the staff member, not the manager");
        asManager.CanCancel.Should().BeTrue();
    }
}
