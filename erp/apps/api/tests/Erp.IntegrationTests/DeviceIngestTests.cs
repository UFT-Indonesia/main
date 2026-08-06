using System.Net;
using System.Net.Http.Json;
using System.Text;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.DeviceIngest;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Erp.IntegrationTests;

/// <summary>
/// PR4 end to end: a punch is now authenticated by the signing device's own key, and a
/// resent request must not become a second punch.
/// </summary>
public class DeviceIngestTests : IntegrationTestBase
{
    private const string DeviceKey = "esp32-front-door";

    public DeviceIngestTests(ErpApiFactory factory) : base(factory) { }

    private sealed record DeviceResponse(Guid Id, string DeviceKey, string Name, bool Enabled);

    private sealed record RegisterResponse(DeviceResponse Device, string Secret);

    private sealed record DeviceList(DeviceResponse[] Items);

    private sealed record PunchResponse(Guid Id, Guid EmployeeId, string Source);

    private async Task<string> RegisterDeviceAsync(HttpClient ownerClient, string key = DeviceKey)
    {
        var response = await ownerClient.PostAsJsonAsync(
            "/api/attendance/devices", new { deviceKey = key, name = "Front door reader" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        return body!.Secret;
    }

    /// <summary>Posts a signed punch exactly as a physical reader would.</summary>
    private async Task<HttpResponseMessage> PunchAsync(
        Guid employeeId,
        string secret,
        DateTimeOffset punchedAt,
        string deviceKey = DeviceKey,
        Instant? signedAt = null)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            employeeId,
            punchedAtUtc = punchedAt,
            punchType = "In",
            deviceId = deviceKey,
        });

        var timestamp = (signedAt ?? SystemClock.Instance.GetCurrentInstant())
            .ToUnixTimeSeconds().ToString();
        var signature = DeviceIngestSignatureValidator.ComputeSignature(payload, timestamp, secret);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/attendance/device-logs")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Device-Timestamp", timestamp);
        request.Headers.Add("X-Device-Signature", signature);

        return await Factory.CreateClient().SendAsync(request);
    }

    [Fact]
    public async Task A_registered_device_can_submit_a_punch()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);
        var ownerClient = await CreateClientForAsync(owner);
        var secret = await RegisterDeviceAsync(ownerClient);

        var response = await PunchAsync(staff.Id.Value, secret, DateTimeOffset.UtcNow);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task An_unregistered_device_is_rejected()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);

        var response = await PunchAsync(
            staff.Id.Value, "a-secret-nobody-registered", DateTimeOffset.UtcNow);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task One_devices_secret_cannot_sign_for_another_device()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);
        var ownerClient = await CreateClientForAsync(owner);
        var frontSecret = await RegisterDeviceAsync(ownerClient, "esp32-front");
        await RegisterDeviceAsync(ownerClient, "esp32-back");

        // Signing as the back door using the front door's key must fail.
        var response = await PunchAsync(
            staff.Id.Value, frontSecret, DateTimeOffset.UtcNow, deviceKey: "esp32-back");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_disabled_device_stops_being_accepted()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);
        var ownerClient = await CreateClientForAsync(owner);
        var secret = await RegisterDeviceAsync(ownerClient);

        var devices = await ownerClient.GetFromJsonAsync<DeviceList>("/api/attendance/devices");
        var deviceId = devices!.Items.Single().Id;
        var disable = await ownerClient.PatchAsJsonAsync(
            $"/api/attendance/devices/{deviceId}/enabled", new { enabled = false });
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await PunchAsync(staff.Id.Value, secret, DateTimeOffset.UtcNow);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_stale_signature_is_rejected()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);
        var ownerClient = await CreateClientForAsync(owner);
        var secret = await RegisterDeviceAsync(ownerClient);

        var response = await PunchAsync(
            staff.Id.Value,
            secret,
            DateTimeOffset.UtcNow,
            signedAt: SystemClock.Instance.GetCurrentInstant().Minus(Duration.FromMinutes(10)));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Replaying_the_same_punch_does_not_create_a_duplicate()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);
        var ownerClient = await CreateClientForAsync(owner);
        var secret = await RegisterDeviceAsync(ownerClient);

        // Same instant, same device — exactly what a network retry resends.
        var punchedAt = DateTimeOffset.UtcNow;
        var first = await PunchAsync(staff.Id.Value, secret, punchedAt);
        var second = await PunchAsync(staff.Id.Value, secret, punchedAt);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstBody = await first.Content.ReadFromJsonAsync<PunchResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<PunchResponse>();
        secondBody!.Id.Should().Be(firstBody!.Id, "a replay resolves to the punch that already exists");

        await using var db = Factory.CreateDbContext();
        var employeeId = new EmployeeId(staff.Id.Value);
        (await db.AttendanceLogs.CountAsync(log => log.EmployeeId == employeeId)).Should().Be(1);
    }

    [Fact]
    public async Task Distinct_punches_from_the_same_device_are_both_recorded()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);
        var ownerClient = await CreateClientForAsync(owner);
        var secret = await RegisterDeviceAsync(ownerClient);

        var punchedAt = DateTimeOffset.UtcNow;
        await PunchAsync(staff.Id.Value, secret, punchedAt);
        await PunchAsync(staff.Id.Value, secret, punchedAt.AddMinutes(1));

        await using var db = Factory.CreateDbContext();
        var employeeId = new EmployeeId(staff.Id.Value);
        (await db.AttendanceLogs.CountAsync(log => log.EmployeeId == employeeId)).Should().Be(2);
    }

    [Fact]
    public async Task Only_an_owner_may_manage_devices()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var managerClient = await CreateClientForAsync(manager);

        (await managerClient.GetAsync("/api/attendance/devices"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await managerClient.PostAsJsonAsync(
            "/api/attendance/devices", new { deviceKey = "x", name = "y" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Registering_a_duplicate_device_id_is_refused()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var ownerClient = await CreateClientForAsync(owner);
        await RegisterDeviceAsync(ownerClient);

        var response = await ownerClient.PostAsJsonAsync(
            "/api/attendance/devices", new { deviceKey = DeviceKey, name = "Another reader" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("attendance_device.duplicate_key");
    }

    [Fact]
    public async Task The_device_list_never_returns_secrets()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var ownerClient = await CreateClientForAsync(owner);
        var secret = await RegisterDeviceAsync(ownerClient);

        var raw = await ownerClient.GetStringAsync("/api/attendance/devices");

        raw.Should().NotContain(secret);
        raw.Should().NotContain("secret");
    }
}
