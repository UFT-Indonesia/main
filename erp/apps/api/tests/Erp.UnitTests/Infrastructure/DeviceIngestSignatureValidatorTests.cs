using Erp.Core.Aggregates.Attendance;
using Erp.Infrastructure.DeviceIngest;
using Erp.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Diagnostics.Internal;
using Microsoft.Extensions.Options;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.Infrastructure;

public sealed class DeviceIngestSignatureValidatorTests
{
    private const string DeviceKey = "esp32-front-door";
    private const string Secret = "device-secret-for-tests";
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 11, 8, 0);

    private readonly AppDbContext _db;
    private readonly DeviceIngestSignatureValidator _validator;

    public DeviceIngestSignatureValidatorTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);

        var clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Now);

        _validator = new DeviceIngestSignatureValidator(
            Options.Create(new DeviceIngestOptions { ToleranceSeconds = 300 }),
            _db,
            clock);
    }

    private async Task SeedDeviceAsync(bool enabled = true, string secret = Secret, string key = DeviceKey)
    {
        var device = AttendanceDevice.Register(key, "Front door reader", secret, Guid.NewGuid(), Now);
        if (!enabled)
        {
            device.Disable();
        }

        _db.AttendanceDevices.Add(device);
        await _db.SaveChangesAsync();
    }

    private static string Timestamp(Duration? offset = null) =>
        Now.Plus(offset ?? Duration.Zero).ToUnixTimeSeconds().ToString();

    [Fact]
    public async Task Accepts_a_signature_made_with_the_devices_own_secret()
    {
        await SeedDeviceAsync();
        var payload = "{\"employeeId\":\"14d583f0-c78d-4d9a-934e-af1e6ee523b1\"}";
        var timestamp = Timestamp();
        var signature = DeviceIngestSignatureValidator.ComputeSignature(payload, timestamp, Secret);

        var result = await _validator.ValidateAsync(payload, DeviceKey, timestamp, signature, default);

        result.IsValid.Should().BeTrue();
        result.FailureCode.Should().BeNull();
    }

    [Fact]
    public async Task Rejects_a_signature_made_with_another_devices_secret()
    {
        // The whole point of per-device keys: a leaked key from one reader is useless on another.
        await SeedDeviceAsync(secret: "the-real-secret");
        var payload = "{}";
        var timestamp = Timestamp();
        var signature = DeviceIngestSignatureValidator.ComputeSignature(payload, timestamp, "some-other-devices-secret");

        var result = await _validator.ValidateAsync(payload, DeviceKey, timestamp, signature, default);

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be("device_ingest.signature_invalid");
    }

    [Fact]
    public async Task Rejects_an_unregistered_device()
    {
        var payload = "{}";
        var timestamp = Timestamp();
        var signature = DeviceIngestSignatureValidator.ComputeSignature(payload, timestamp, Secret);

        var result = await _validator.ValidateAsync(payload, "never-registered", timestamp, signature, default);

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be("device_ingest.device_unknown");
    }

    [Fact]
    public async Task Rejects_a_disabled_device_even_with_a_correct_signature()
    {
        await SeedDeviceAsync(enabled: false);
        var payload = "{}";
        var timestamp = Timestamp();
        var signature = DeviceIngestSignatureValidator.ComputeSignature(payload, timestamp, Secret);

        var result = await _validator.ValidateAsync(payload, DeviceKey, timestamp, signature, default);

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be("device_ingest.device_disabled");
    }

    [Fact]
    public async Task Rejects_a_stale_timestamp()
    {
        await SeedDeviceAsync();
        var payload = "{}";
        var timestamp = Timestamp(Duration.FromMinutes(-10));
        var signature = DeviceIngestSignatureValidator.ComputeSignature(payload, timestamp, Secret);

        var result = await _validator.ValidateAsync(payload, DeviceKey, timestamp, signature, default);

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be("device_ingest.timestamp_out_of_tolerance");
    }

    [Fact]
    public async Task Rejects_a_future_timestamp_beyond_tolerance()
    {
        await SeedDeviceAsync();
        var payload = "{}";
        var timestamp = Timestamp(Duration.FromMinutes(10));
        var signature = DeviceIngestSignatureValidator.ComputeSignature(payload, timestamp, Secret);

        var result = await _validator.ValidateAsync(payload, DeviceKey, timestamp, signature, default);

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be("device_ingest.timestamp_out_of_tolerance");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Requires_a_device_key(string? deviceKey)
    {
        await SeedDeviceAsync();

        var result = await _validator.ValidateAsync("{}", deviceKey, Timestamp(), "signature", default);

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be("device_ingest.device_key_required");
    }

    [Fact]
    public async Task Requires_a_timestamp_and_signature()
    {
        await SeedDeviceAsync();

        (await _validator.ValidateAsync("{}", DeviceKey, null, "sig", default))
            .FailureCode.Should().Be("device_ingest.timestamp_required");
        (await _validator.ValidateAsync("{}", DeviceKey, Timestamp(), null, default))
            .FailureCode.Should().Be("device_ingest.signature_required");
    }

    [Fact]
    public async Task Rejects_a_non_numeric_timestamp()
    {
        await SeedDeviceAsync();

        var result = await _validator.ValidateAsync("{}", DeviceKey, "not-a-number", "sig", default);

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be("device_ingest.timestamp_invalid");
    }

    [Fact]
    public async Task Signing_covers_the_payload_so_a_tampered_body_fails()
    {
        await SeedDeviceAsync();
        var timestamp = Timestamp();
        var signature = DeviceIngestSignatureValidator.ComputeSignature("{\"punchType\":\"In\"}", timestamp, Secret);

        var result = await _validator.ValidateAsync("{\"punchType\":\"Out\"}", DeviceKey, timestamp, signature, default);

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be("device_ingest.signature_invalid");
    }
}
