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
        bool CanCancel,
        string? Reason,
        int? ApprovedWorkdaysThisYear);

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
    public async Task The_list_is_a_company_wide_calendar_every_role_can_read()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);
        var colleague = await CreateEmployeeAsync(EmployeeRole.Staff, "Rekan Kerja", manager.Id);

        var ownerClient = await CreateClientForAsync(owner);
        await ownerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(staff.Id.Value));
        await ownerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(colleague.Id.Value));
        await ownerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(owner.Id.Value));

        // The point of the calendar: staff can tell their own boss is away without asking.
        var staffClient = await CreateClientForAsync(staff);
        var list = await staffClient.GetFromJsonAsync<LeaveList>("/api/leave/?status=");

        list!.Items.Select(item => item.EmployeeId)
            .Should().BeEquivalentTo([staff.Id.Value, colleague.Id.Value, owner.Id.Value]);
        list.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Staff_see_that_a_colleague_is_away_but_never_why()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);
        var colleague = await CreateEmployeeAsync(EmployeeRole.Staff, "Rekan Kerja", manager.Id);

        var ownerClient = await CreateClientForAsync(owner);
        await ownerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(colleague.Id.Value));

        var staffClient = await CreateClientForAsync(staff);
        await staffClient.PostAsJsonAsync("/api/leave/", NewRequestFor(staff.Id.Value));

        var list = await staffClient.GetFromJsonAsync<LeaveList>("/api/leave/?status=");

        list!.Items.Single(item => item.EmployeeId == staff.Id.Value)
            .Reason.Should().Be("cuti", "it is their own request");
        list.Items.Single(item => item.EmployeeId == colleague.Id.Value)
            .Reason.Should().BeNull("the reason is private to the employee and their approvers");
    }

    [Fact]
    public async Task A_leave_balance_is_hidden_from_everyone_the_employee_does_not_answer_to()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);

        var ownerClient = await CreateClientForAsync(owner);
        // An Owner's own leave is approved on filing, so it lands in their yearly balance.
        await ownerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(owner.Id.Value));
        await ownerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(staff.Id.Value));

        var staffList = await (await CreateClientForAsync(staff))
            .GetFromJsonAsync<LeaveList>("/api/leave/?status=");
        staffList!.Items.Single(item => item.EmployeeId == staff.Id.Value)
            .ApprovedWorkdaysThisYear.Should().NotBeNull("their own balance is theirs to see");
        staffList.Items.Single(item => item.EmployeeId == owner.Id.Value)
            .ApprovedWorkdaysThisYear.Should().BeNull("staff do not tally anyone else's leave");

        var managerList = await (await CreateClientForAsync(manager))
            .GetFromJsonAsync<LeaveList>("/api/leave/?status=");
        managerList!.Items.Single(item => item.EmployeeId == staff.Id.Value)
            .ApprovedWorkdaysThisYear.Should().NotBeNull("a manager plans cover across the staff");
        managerList.Items.Single(item => item.EmployeeId == owner.Id.Value)
            .ApprovedWorkdaysThisYear.Should().BeNull("nobody below the owner tallies the owner");
    }

    [Fact]
    public async Task A_manager_reads_the_reason_only_inside_their_own_line()
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

        // Every row is visible — the calendar is company-wide — but the free text is not.
        list!.Items.Select(item => item.EmployeeId).Should().BeEquivalentTo(
            [ownStaff.Id.Value, foreignStaff.Id.Value, owner.Id.Value]);

        list.Items.Single(item => item.EmployeeId == ownStaff.Id.Value)
            .Reason.Should().Be("cuti", "a manager decides their own staff's leave");
        list.Items.Single(item => item.EmployeeId == foreignStaff.Id.Value)
            .Reason.Should().BeNull("another manager's line is not theirs to read");
        list.Items.Single(item => item.EmployeeId == owner.Id.Value)
            .Reason.Should().BeNull("nobody below the owner reads the owner's reason");
    }

    [Fact]
    public async Task An_owner_reads_every_reason_including_another_owners()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var coOwner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Kedua");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);

        var coOwnerClient = await CreateClientForAsync(coOwner);
        await coOwnerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(coOwner.Id.Value));
        await coOwnerClient.PostAsJsonAsync("/api/leave/", NewRequestFor(manager.Id.Value));

        var list = await (await CreateClientForAsync(owner))
            .GetFromJsonAsync<LeaveList>("/api/leave/?status=");

        list!.Items.Should().OnlyContain(item => item.Reason == "cuti");
        list.Items.Should().OnlyContain(item => item.ApprovedWorkdaysThisYear != null);
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
