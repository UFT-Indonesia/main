using Erp.Core.Aggregates.Attendance;
using Erp.Infrastructure.DeviceIngest;
using Erp.Infrastructure.Persistence;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Diagnostics.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.Infrastructure;

/// <summary>
/// The backfill is what stops this deployment from bricking every physical reader, so its
/// idempotency and its no-secret fallback both matter.
/// </summary>
public sealed class AttendanceDeviceSeederTests
{
    private const string LegacySecret = "legacy-shared-secret";
    private static readonly Instant Now = Instant.FromUtc(2026, 8, 6, 9, 0);

    private readonly AppDbContext _db;

    public AttendanceDeviceSeederTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);
    }

    private AttendanceDeviceSeeder CreateSeeder(string secret = LegacySecret)
    {
        var clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Now);

        return new AttendanceDeviceSeeder(
            _db,
            clock,
            Options.Create(new DeviceIngestOptions { HmacSecret = secret, ToleranceSeconds = 300 }),
            NullLogger<AttendanceDeviceSeeder>.Instance);
    }

    private async Task SeedPunchAsync(string deviceId)
    {
        _db.AttendanceLogs.Add(AttendanceLog.FromDevice(
            EmployeeId.New(), Now, PunchType.In, deviceId));
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Registers_a_device_for_every_device_id_seen_in_punch_history()
    {
        await SeedPunchAsync("esp32-front");
        await SeedPunchAsync("esp32-back");

        await CreateSeeder().SeedAsync();

        var devices = await _db.AttendanceDevices.ToListAsync();
        devices.Select(d => d.DeviceKey).Should().BeEquivalentTo(["esp32-front", "esp32-back"]);
        devices.Should().OnlyContain(d => d.Enabled && d.Secret == LegacySecret);
    }

    [Fact]
    public async Task Backfilled_devices_keep_working_with_the_outgoing_shared_secret()
    {
        // The point of reusing the old secret: no reader needs reflashing to survive deploy.
        await SeedPunchAsync("esp32-front");

        await CreateSeeder().SeedAsync();

        var device = await _db.AttendanceDevices.SingleAsync();
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var signature = DeviceIngestSignatureValidator.ComputeSignature("{}", timestamp, device.Secret);

        signature.Should().Be(DeviceIngestSignatureValidator.ComputeSignature("{}", timestamp, LegacySecret));
    }

    [Fact]
    public async Task Running_twice_does_not_duplicate_devices()
    {
        await SeedPunchAsync("esp32-front");

        await CreateSeeder().SeedAsync();
        await CreateSeeder().SeedAsync();

        (await _db.AttendanceDevices.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Leaves_already_registered_devices_untouched()
    {
        _db.AttendanceDevices.Add(AttendanceDevice.Register(
            "esp32-front", "Proper name", "its-own-secret", Guid.NewGuid(), Now));
        await _db.SaveChangesAsync();
        await SeedPunchAsync("esp32-front");

        await CreateSeeder().SeedAsync();

        var device = await _db.AttendanceDevices.SingleAsync();
        device.Name.Should().Be("Proper name");
        device.Secret.Should().Be("its-own-secret");
    }

    [Fact]
    public async Task Ignores_manual_punches_which_have_no_device()
    {
        _db.AttendanceLogs.Add(AttendanceLog.Manual(
            EmployeeId.New(), Now, PunchType.In, Guid.NewGuid()));
        await _db.SaveChangesAsync();

        await CreateSeeder().SeedAsync();

        (await _db.AttendanceDevices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Registers_nothing_when_no_legacy_secret_is_configured()
    {
        // Nothing sane to backfill with — the operator must register these by hand.
        await SeedPunchAsync("esp32-front");

        await CreateSeeder(secret: string.Empty).SeedAsync();

        (await _db.AttendanceDevices.CountAsync()).Should().Be(0);
    }
}
