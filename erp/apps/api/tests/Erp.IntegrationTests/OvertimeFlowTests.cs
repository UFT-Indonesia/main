using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Erp.IntegrationTests;

/// <summary>
/// GSS08 end to end on a real database — chiefly what unit tests cannot reach: that every query
/// the use cases build translates to SQL, that scoping holds per caller, and that closing a
/// period really locks it. Dates sit in the Jul–Aug 2026 period, which has ended by the time
/// these run, so it can be closed.
/// </summary>
public class OvertimeFlowTests : IntegrationTestBase
{
    private const string WorkDate = "2026-07-07"; // a Tuesday
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    public OvertimeFlowTests(ErpApiFactory factory) : base(factory) { }

    private sealed record OvertimeItem(
        Guid Id, string EmployeeFullName, string Status, bool IsDayOff, int Hours, decimal? Amount, bool Incomplete,
        bool CanDecide, bool CanManage, bool CanFileCorrection, bool IsFrozen);

    private sealed record OvertimeList(OvertimeItem[] Items, int TotalCount);

    private sealed record CorrectionItem(Guid Id, string Status, bool CanDecide);

    private sealed record PeriodRow(string FullName, int Hours1, decimal Total);

    private sealed record Period(bool Closed, bool CanClose, decimal Total, PeriodRow[] Rows);

    private sealed record MyRow(string Start, bool Closed, bool CanRequestRapel, decimal Total, RapelItem[] RapelPaid);

    private sealed record RapelItem(Guid Id, string Status, decimal? Amount, int ClaimedHours, decimal SuggestedAmount);

    private async Task<(Employee Owner, Employee Manager, Employee Staff, Employee Foreign)> TeamAsync()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var other = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Dua", owner.Id);
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", manager.Id);
        var foreign = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Lain", other.Id);
        return (owner, manager, staff, foreign);
    }

    private static object Weekday(Guid employeeId, string end = "21:30", string date = WorkDate) =>
        new { employeeId, date, endTime = end };

    private async Task PunchAsync(Employee employee, string jakartaDateTime, PunchType type)
    {
        var at = LocalDateTime.FromDateTime(DateTime.Parse(jakartaDateTime))
            .InZoneLeniently(DateTimeZoneProviders.Tzdb["Asia/Jakarta"]).ToInstant();
        await using var db = Factory.CreateDbContext();
        db.AttendanceLogs.Add(AttendanceLog.FromDevice(employee.Id, at, type, "dev-1"));
        await db.SaveChangesAsync();
    }

    private static async Task<OvertimeItem> OkAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<OvertimeItem>())!;
    }

    [Fact]
    public async Task A_manager_assigns_only_to_own_staff_and_an_owner_approves()
    {
        var (owner, manager, staff, foreign) = await TeamAsync();
        var managerClient = await CreateClientForAsync(manager);
        var ownerClient = await CreateClientForAsync(owner);

        (await managerClient.PostAsJsonAsync("/api/overtime/", Weekday(foreign.Id.Value)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var pending = await OkAsync(await managerClient.PostAsJsonAsync("/api/overtime/", Weekday(staff.Id.Value)));
        pending.Status.Should().Be("Pending");
        pending.CanDecide.Should().BeFalse();

        (await managerClient.PostAsJsonAsync($"/api/overtime/{pending.Id}/approve", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await OkAsync(await ownerClient.PostAsJsonAsync($"/api/overtime/{pending.Id}/approve", new { })))
            .Status.Should().Be("Approved");
    }

    [Fact]
    public async Task Listing_is_scoped_by_role_and_pay_is_hidden_from_a_manager()
    {
        var (owner, manager, staff, foreign) = await TeamAsync();
        var ownerClient = await CreateClientForAsync(owner);
        await OkAsync(await ownerClient.PostAsJsonAsync("/api/overtime/", Weekday(staff.Id.Value)));
        await OkAsync(await ownerClient.PostAsJsonAsync("/api/overtime/", Weekday(foreign.Id.Value)));
        await PunchAsync(staff, $"{WorkDate} 18:31", PunchType.In);
        await PunchAsync(staff, $"{WorkDate} 21:35", PunchType.Out);

        var all = await (await ownerClient.GetAsync("/api/overtime/")).Content.ReadFromJsonAsync<OvertimeList>();
        all!.TotalCount.Should().Be(2);
        all.Items.Single(i => i.EmployeeFullName == "Staff Biasa").Amount.Should().Be(25_000);

        var seenByManager = await (await (await CreateClientForAsync(manager)).GetAsync("/api/overtime/"))
            .Content.ReadFromJsonAsync<OvertimeList>();
        seenByManager!.Items.Should().ContainSingle().Which.Amount.Should().BeNull();

        var seenByStaff = await (await (await CreateClientForAsync(staff)).GetAsync("/api/overtime/"))
            .Content.ReadFromJsonAsync<OvertimeList>();
        var own = seenByStaff!.Items.Should().ContainSingle().Subject;
        own.Hours.Should().Be(3);
        own.Amount.Should().Be(25_000);
    }

    [Fact]
    public async Task Leave_cannot_be_filed_on_a_date_with_overtime()
    {
        var (owner, _, staff, _) = await TeamAsync();
        await OkAsync(await (await CreateClientForAsync(owner)).PostAsJsonAsync("/api/overtime/", Weekday(staff.Id.Value)));

        // Leave is multipart (Sick carries a file), so even a plain request goes as a form.
        using var form = new MultipartFormDataContent
        {
            { new StringContent(staff.Id.Value.ToString()), "EmployeeId" },
            { new StringContent("Annual"), "Type" },
            { new StringContent(WorkDate), "StartDate" },
            { new StringContent(WorkDate), "EndDate" },
            { new StringContent("cuti"), "Reason" },
        };
        var response = await (await CreateClientForAsync(staff)).PostAsync("/api/leave/", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("leave.overtime_on_date");
    }

    [Fact]
    public async Task A_missing_tap_out_can_be_corrected_on_request_and_only_the_manager_decides()
    {
        var (owner, manager, staff, _) = await TeamAsync();
        var ownerClient = await CreateClientForAsync(owner);
        var staffClient = await CreateClientForAsync(staff);
        var item = await OkAsync(await ownerClient.PostAsJsonAsync("/api/overtime/", Weekday(staff.Id.Value)));
        await PunchAsync(staff, $"{WorkDate} 18:31", PunchType.In);

        var mine = (await (await staffClient.GetAsync("/api/overtime/")).Content.ReadFromJsonAsync<OvertimeList>())!.Items.Single();
        mine.Incomplete.Should().BeTrue();
        mine.Amount.Should().Be(0);
        mine.CanFileCorrection.Should().BeTrue();

        using var form = new MultipartFormDataContent
        {
            { new StringContent(item.Id.ToString()), "AssignmentId" },
            { new StringContent("TapOut"), "Kind" },
            { new StringContent("21:30"), "Time" },
            { new StringContent("lupa tap out"), "Reason" },
        };
        var proof = new ByteArrayContent(Png);
        proof.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(proof, "Attachment", "bukti.png");
        var filed = await staffClient.PostAsync("/api/overtime/corrections", form);
        filed.StatusCode.Should().Be(HttpStatusCode.OK, await filed.Content.ReadAsStringAsync());
        var correction = (await filed.Content.ReadFromJsonAsync<CorrectionItem>())!;

        (await staffClient.PostAsJsonAsync($"/api/overtime/corrections/{correction.Id}/approve", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Approving writes a punch and publishes it through Wolverine, whose envelope tables this
        // test database does not have (the same reason AttendanceTransactionTests' manual-punch test
        // fails here), so the approve path is covered by OvertimeHandlersTests instead.
        var managerClient = await CreateClientForAsync(manager);
        var rejected = await managerClient.PostAsJsonAsync($"/api/overtime/corrections/{correction.Id}/reject", new { note = "bukti kurang jelas" });
        rejected.StatusCode.Should().Be(HttpStatusCode.OK, await rejected.Content.ReadAsStringAsync());
        (await rejected.Content.ReadFromJsonAsync<CorrectionItem>())!.Status.Should().Be("Rejected");

        await using var db = Factory.CreateDbContext();
        (await db.AttendanceLogs.CountAsync(l => l.EmployeeId == staff.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Closing_a_period_freezes_it_refuses_new_overtime_and_lets_a_rapel_in()
    {
        var (owner, _, staff, _) = await TeamAsync();
        var ownerClient = await CreateClientForAsync(owner);
        await OkAsync(await ownerClient.PostAsJsonAsync("/api/overtime/", Weekday(staff.Id.Value)));
        await PunchAsync(staff, $"{WorkDate} 18:31", PunchType.In);
        await PunchAsync(staff, $"{WorkDate} 21:35", PunchType.Out);
        // A second evening with the tap-out never recorded: it closes at Rp0 and comes back as a rapel.
        var forgotten = await OkAsync(await ownerClient.PostAsJsonAsync("/api/overtime/", Weekday(staff.Id.Value, date: "2026-07-14")));
        await PunchAsync(staff, "2026-07-14 18:31", PunchType.In);

        var open = await (await ownerClient.GetAsync("/api/payroll/gaji-premi?periodStart=2026-07-01")).Content.ReadFromJsonAsync<Period>();
        open!.Closed.Should().BeFalse();
        open.CanClose.Should().BeTrue();
        open.Rows.Should().ContainSingle().Which.Total.Should().Be(25_000);

        var staffClient = await CreateClientForAsync(staff);
        (await staffClient.PostAsJsonAsync("/api/payroll/gaji-premi/close", new { periodStart = "2026-07-01" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var closed = await ownerClient.PostAsJsonAsync("/api/payroll/gaji-premi/close", new { periodStart = "2026-07-01" });
        closed.StatusCode.Should().Be(HttpStatusCode.OK, await closed.Content.ReadAsStringAsync());
        (await closed.Content.ReadFromJsonAsync<Period>())!.Closed.Should().BeTrue();

        (await ownerClient.PostAsJsonAsync("/api/overtime/", Weekday(staff.Id.Value, date: "2026-07-08")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The request deadline runs a month past the Sep 15 payout, so pin "today" inside it.
        await using var host = Pinned(SeptemberTwentieth);
        var (staffAt, ownerAt) = (As(host, staffClient), As(host, ownerClient));

        // The employee claims the forgotten evening with the times worked; the system suggests the amount.
        var request = await staffAt.PostAsync("/api/overtime/rapel", RapelForm(forgotten.Id, "18:30", "21:30"));
        request.StatusCode.Should().Be(HttpStatusCode.OK, await request.Content.ReadAsStringAsync());
        var rapel = (await request.Content.ReadFromJsonAsync<RapelItem>())!;
        rapel.Should().Match<RapelItem>(r => r.Status == "Pending" && r.ClaimedHours == 3 && r.SuggestedAmount == 25_000);

        (await staffAt.PostAsync("/api/overtime/rapel", RapelForm(forgotten.Id, "18:30", "21:30")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "one rapel per overtime");

        (await staffAt.PostAsJsonAsync($"/api/payroll/rapel/{rapel.Id}/approve", new { amount = 50_000 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var approved = await ownerAt.PostAsJsonAsync($"/api/payroll/rapel/{rapel.Id}/approve", new { });
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        (await approved.Content.ReadFromJsonAsync<RapelItem>())!.Amount.Should().Be(25_000, "no amount keeps the suggestion");

        var mine = await (await staffAt.GetAsync("/api/overtime/gaji-premi/me")).Content.ReadFromJsonAsync<MyRow[]>();
        mine!.Single(r => r.Start == "2026-07-01").Should().Match<MyRow>(r => r.Closed && r.CanRequestRapel && r.Total == 25_000);
        var payout = mine!.Single(r => r.Start == "2026-09-01");
        payout.Total.Should().Be(25_000);
        payout.RapelPaid.Should().ContainSingle();
    }

    [Fact]
    public async Task A_rapel_request_after_the_deadline_is_refused_but_the_owner_can_still_add_one()
    {
        var (owner, _, staff, _) = await TeamAsync();
        var ownerClient = await CreateClientForAsync(owner);
        var forgotten = await OkAsync(await ownerClient.PostAsJsonAsync("/api/overtime/", Weekday(staff.Id.Value)));
        await PunchAsync(staff, $"{WorkDate} 18:31", PunchType.In);
        (await ownerClient.PostAsJsonAsync("/api/payroll/gaji-premi/close", new { periodStart = "2026-07-01" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Payout Sep 15 → requests close Oct 15.
        await using var host = Pinned(Clock(2026, 10, 16));
        var (staffAt, ownerAt) = (As(host, await CreateClientForAsync(staff)), As(host, ownerClient));

        var late = await staffAt.PostAsync("/api/overtime/rapel", RapelForm(forgotten.Id, "18:30", "21:30"));
        late.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await late.Content.ReadAsStringAsync()).Should().Contain("rapel.deadline_passed");

        var mine = await (await staffAt.GetAsync("/api/overtime/gaji-premi/me")).Content.ReadFromJsonAsync<MyRow[]>();
        mine!.Single(r => r.Start == "2026-07-01").Should().Match<MyRow>(r => r.Closed && !r.CanRequestRapel);

        var added = await ownerAt.PostAsync("/api/overtime/rapel", RapelForm(forgotten.Id, "18:30", "21:30"));
        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
        (await added.Content.ReadFromJsonAsync<RapelItem>())!.Should().Match<RapelItem>(r => r.Status == "Approved" && r.Amount == 25_000);
    }

    private static readonly Action<IServiceCollection> SeptemberTwentieth = Clock(2026, 9, 20);

    /// <summary>A second host over the same database with the clock swapped; the caller disposes it.</summary>
    private WebApplicationFactory<Program> Pinned(Action<IServiceCollection> clock) =>
        Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(clock));

    /// <summary>A client on <paramref name="host"/> carrying the token of one from the shared host — both sign with the same test key.</summary>
    private static HttpClient As(WebApplicationFactory<Program> host, HttpClient signedIn)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;
        return client;
    }

    /// <summary>Pins the host's "today" (10:00 Jakarta) for deadline checks; tokens and HMAC stay on real time.</summary>
    private static Action<IServiceCollection> Clock(int year, int month, int day) =>
        services => services.AddSingleton<IClock>(new FixedClock(Instant.FromUtc(year, month, day, 3, 0)));

    private sealed class FixedClock(Instant now) : IClock
    {
        public Instant GetCurrentInstant() => now;
    }

    private static MultipartFormDataContent RapelForm(Guid assignmentId, string from, string to)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(assignmentId.ToString()), "AssignmentId" },
            { new StringContent(from), "From" },
            { new StringContent(to), "To" },
            { new StringContent("lupa tap out, ada chat WA"), "Note" },
        };
        var proof = new ByteArrayContent(Png);
        proof.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(proof, "Attachment", "wa.png");
        return form;
    }

    [Fact]
    public async Task Day_markers_flag_overtime_and_leave_dates_as_bare_kinds_and_the_calendar_shows_the_overtime()
    {
        var (owner, _, staff, _) = await TeamAsync();
        var ownerClient = await CreateClientForAsync(owner);
        await OkAsync(await ownerClient.PostAsJsonAsync("/api/overtime/", Weekday(staff.Id.Value)));
        await PunchAsync(staff, $"{WorkDate} 18:31", PunchType.In);
        await PunchAsync(staff, $"{WorkDate} 21:35", PunchType.Out);

        // Anyone signed in may ask — the staff member's own client, here.
        var staffClient = await CreateClientForAsync(staff);
        var markers = await staffClient.GetFromJsonAsync<System.Text.Json.JsonElement>(
            $"/api/calendar/markers?employeeId={staff.Id.Value}&from=2026-07-01&to=2026-07-31");
        var marked = markers.GetProperty("markers").EnumerateArray()
            .Select(m => (Date: m.GetProperty("date").GetString(), Kind: m.GetProperty("kind").GetString())).ToList();
        marked.Should().Contain((WorkDate, "Overtime"));

        var calendar = await staffClient.GetFromJsonAsync<System.Text.Json.JsonElement>(
            $"/api/attendance/days?from={WorkDate}&to={WorkDate}");
        var row = calendar.GetProperty("dates")[0].GetProperty("employees").EnumerateArray()
            .Single(e => e.GetProperty("employeeId").GetGuid() == staff.Id.Value);
        row.GetProperty("overtimeStatus").GetString().Should().Be("Approved");
        row.GetProperty("overtimeHours").GetInt32().Should().Be(3);
        // The 18:31 punch is the OT's, so the regular day is Absent rather than Incomplete.
        row.GetProperty("status").GetString().Should().Be("Absent");
    }
}
