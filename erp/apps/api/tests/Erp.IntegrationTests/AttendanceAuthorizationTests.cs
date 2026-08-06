using System.Net;
using System.Net.Http.Json;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using NodaTime;

namespace Erp.IntegrationTests;

/// <summary>
/// PR3 rules end to end. Reads are wider than writes on purpose: a Manager sees the whole
/// company's times but may only alter their own reporting line.
/// </summary>
public class AttendanceAuthorizationTests : IntegrationTestBase
{
    public AttendanceAuthorizationTests(ErpApiFactory factory) : base(factory) { }

    private sealed record LogItem(Guid Id, Guid EmployeeId, bool CanWrite);

    private sealed record LogList(LogItem[] Items, int TotalCount);

    private async Task SeedPunchAsync(EmployeeId employeeId, Instant at)
    {
        await using var db = Factory.CreateDbContext();
        db.AttendanceLogs.Add(AttendanceLog.FromDevice(employeeId, at, PunchType.In, "esp32-test"));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Staff_see_only_their_own_punches()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);
        var colleague = await CreateEmployeeAsync(EmployeeRole.Staff, "Rekan Kerja", manager.Id);

        var now = SystemClock.Instance.GetCurrentInstant();
        await SeedPunchAsync(staff.Id, now);
        await SeedPunchAsync(colleague.Id, now);

        var client = await CreateClientForAsync(staff);
        var list = await client.GetFromJsonAsync<LogList>("/api/attendance/");

        list!.Items.Should().OnlyContain(item => item.EmployeeId == staff.Id.Value);
        list.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task A_manager_sees_the_whole_company()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var outsider = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Lain", owner.Id);
        var foreignStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Asing", outsider.Id);

        var now = SystemClock.Instance.GetCurrentInstant();
        await SeedPunchAsync(foreignStaff.Id, now);

        var client = await CreateClientForAsync(manager);
        var list = await client.GetFromJsonAsync<LogList>("/api/attendance/");

        list!.Items.Should().Contain(item => item.EmployeeId == foreignStaff.Id.Value);
    }

    [Fact]
    public async Task Read_access_does_not_imply_write_access_for_a_manager()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var outsider = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Lain", owner.Id);
        var ownStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Anak Buah", manager.Id);
        var foreignStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Asing", outsider.Id);

        var now = SystemClock.Instance.GetCurrentInstant();
        await SeedPunchAsync(ownStaff.Id, now);
        await SeedPunchAsync(foreignStaff.Id, now);

        var client = await CreateClientForAsync(manager);
        var list = await client.GetFromJsonAsync<LogList>("/api/attendance/");

        list!.Items.Single(item => item.EmployeeId == ownStaff.Id.Value).CanWrite.Should().BeTrue();
        list.Items.Single(item => item.EmployeeId == foreignStaff.Id.Value).CanWrite.Should().BeFalse();
    }

    [Fact]
    public async Task A_manager_can_record_a_manual_punch_for_their_own_staff()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Anak Buah", manager.Id);

        var client = await CreateClientForAsync(manager);
        var response = await client.PostAsJsonAsync("/api/attendance/manual-logs", new
        {
            employeeId = staff.Id.Value,
            punchedAtUtc = DateTimeOffset.UtcNow,
            punchType = "In",
            note = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_manager_cannot_record_a_manual_punch_outside_their_line()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var outsider = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Lain", owner.Id);
        var foreignStaff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Asing", outsider.Id);

        var client = await CreateClientForAsync(manager);
        var response = await client.PostAsJsonAsync("/api/attendance/manual-logs", new
        {
            employeeId = foreignStaff.Id.Value,
            punchedAtUtc = DateTimeOffset.UtcNow,
            punchType = "In",
            note = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_manager_cannot_fabricate_attendance_for_the_owner()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);

        var client = await CreateClientForAsync(manager);
        var response = await client.PostAsJsonAsync("/api/attendance/manual-logs", new
        {
            employeeId = owner.Id.Value,
            punchedAtUtc = DateTimeOffset.UtcNow,
            punchType = "In",
            note = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Staff_cannot_record_attendance_at_all()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);

        var client = await CreateClientForAsync(staff);
        var response = await client.PostAsJsonAsync("/api/attendance/manual-logs", new
        {
            employeeId = staff.Id.Value,
            punchedAtUtc = DateTimeOffset.UtcNow,
            punchType = "In",
            note = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Staff_cannot_open_another_employees_day()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);
        var colleague = await CreateEmployeeAsync(EmployeeRole.Staff, "Rekan Kerja", manager.Id);

        var client = await CreateClientForAsync(staff);
        var response = await client.GetAsync(
            $"/api/attendance/days/{colleague.Id.Value}/2026-08-05/logs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Staff_cannot_export_someone_elses_attendance()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);
        var colleague = await CreateEmployeeAsync(EmployeeRole.Staff, "Rekan Kerja", manager.Id);

        var client = await CreateClientForAsync(staff);
        var response = await client.PostAsJsonAsync("/api/attendance/days/export", new
        {
            items = new[]
            {
                new { employeeId = staff.Id.Value, date = "2026-08-05" },
                new { employeeId = colleague.Id.Value, date = "2026-08-05" },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_export_refuses_more_keys_than_it_will_process()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");

        var client = await CreateClientForAsync(owner);
        var response = await client.PostAsJsonAsync("/api/attendance/days/export", new
        {
            items = Enumerable.Range(0, 501)
                .Select(i => new { employeeId = owner.Id.Value, date = $"2026-01-{(i % 28) + 1:D2}" })
                .ToArray(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("attendance.export_too_many");
    }
}
