using System.Net;
using System.Net.Http.Json;
using Erp.Core.Aggregates.Employees;
using FluentAssertions;
using NodaTime;

namespace Erp.IntegrationTests;

/// <summary>
/// Who may reach the probation endpoints at all, and what the scoped list returns — the parts
/// the role attributes and the query scope own, which unit tests on the handlers cannot see.
/// </summary>
// NOTE (unverified): these have never been executed — they run on Testcontainers and Docker was
// unavailable when they were written. Run `dotnet test apps/api/tests/Erp.IntegrationTests` with
// Docker up before trusting them.
public class ProbationAuthorizationTests : IntegrationTestBase
{
    public ProbationAuthorizationTests(ErpApiFactory factory) : base(factory) { }

    /// <summary>Far enough out that the fixture stays on probation whenever the suite runs.</summary>
    private static LocalDate RecentHire => SystemClock.Instance.GetCurrentInstant().InUtc().Date;

    private static LocalDate ProposedEnd => RecentHire.PlusMonths(6);

    private sealed record ExtensionItem(Guid Id, Guid EmployeeId, string Status, bool CanDecide, bool CanCancel);

    private sealed record ExtensionList(ExtensionItem[] Items, int TotalCount);

    private static object RequestFor(Guid employeeId) => new
    {
        employeeId,
        proposedEndsOn = ProposedEnd.ToDateOnly().ToString("yyyy-MM-dd"),
        reason = "Perlu waktu tambahan untuk penilaian.",
    };

    [Fact]
    public async Task A_manager_can_file_for_their_own_probationary_staff()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Baru", manager.Id, RecentHire);

        var client = await CreateClientForAsync(manager);
        var response = await client.PostAsJsonAsync("/api/probation/", RequestFor(staff.Id.Value));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task An_owner_cannot_file_they_hold_the_direct_edit()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Baru", manager.Id, RecentHire);

        var client = await CreateClientForAsync(owner);
        var response = await client.PostAsJsonAsync("/api/probation/", RequestFor(staff.Id.Value));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Staff_cannot_reach_the_probation_endpoints_at_all()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Baru", manager.Id, RecentHire);

        var client = await CreateClientForAsync(staff);

        (await client.GetAsync("/api/probation/")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/probation/", RequestFor(staff.Id.Value)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_manager_only_sees_their_own_staffs_requests()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var outsider = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Lain", owner.Id);
        var ownStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Anak Buah", manager.Id, RecentHire);
        var foreignStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Lain", outsider.Id, RecentHire);

        var managerClient = await CreateClientForAsync(manager);
        var outsiderClient = await CreateClientForAsync(outsider);
        await managerClient.PostAsJsonAsync("/api/probation/", RequestFor(ownStaff.Id.Value));
        await outsiderClient.PostAsJsonAsync("/api/probation/", RequestFor(foreignStaff.Id.Value));

        var mine = await managerClient.GetFromJsonAsync<ExtensionList>("/api/probation/");
        mine!.Items.Should().OnlyContain(item => item.EmployeeId == ownStaff.Id.Value);

        var ownerClient = await CreateClientForAsync(owner);
        var all = await ownerClient.GetFromJsonAsync<ExtensionList>("/api/probation/");
        all!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Approval_is_owner_only_and_moves_the_probation_end()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Baru", manager.Id, RecentHire);

        var managerClient = await CreateClientForAsync(manager);
        var created = await managerClient.PostAsJsonAsync("/api/probation/", RequestFor(staff.Id.Value));
        created.EnsureSuccessStatusCode();
        var request = (await created.Content.ReadFromJsonAsync<ExtensionItem>())!;

        var byManager = await managerClient.PostAsJsonAsync(
            $"/api/probation/{request.Id}/approve", new { note = (string?)null });
        byManager.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ownerClient = await CreateClientForAsync(owner);
        var byOwner = await ownerClient.PostAsJsonAsync(
            $"/api/probation/{request.Id}/approve", new { note = (string?)null });
        byOwner.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = Factory.CreateDbContext();
        var updated = await db.Employees.FindAsync(staff.Id);
        updated!.ProbationEndsOnOverride.Should().Be(ProposedEnd);
    }

    [Fact]
    public async Task Only_an_owner_may_edit_the_probation_end_or_a_quota_directly()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Baru", manager.Id, RecentHire);

        var managerClient = await CreateClientForAsync(manager);
        (await managerClient.PutAsJsonAsync(
                $"/api/employees/{staff.Id.Value}/probation", new { endsOn = "2027-01-01" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await managerClient.PutAsJsonAsync(
                $"/api/employees/{staff.Id.Value}/quota", new { type = "Annual", days = 20 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ownerClient = await CreateClientForAsync(owner);
        (await ownerClient.PutAsJsonAsync(
                $"/api/employees/{staff.Id.Value}/probation", new { endsOn = "2027-01-01" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await ownerClient.PutAsJsonAsync(
                $"/api/employees/{staff.Id.Value}/quota", new { type = "Annual", days = 20 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
