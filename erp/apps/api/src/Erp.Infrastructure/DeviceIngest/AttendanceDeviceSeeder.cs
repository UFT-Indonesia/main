using Erp.Core.Aggregates.Attendance;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Erp.Infrastructure.DeviceIngest;

/// <summary>
/// Runs once at startup. Signature validation moved from one shared secret to a per-device
/// one, so every device id already seen in punch history needs a registry row before this
/// deploys, or every existing physical reader starts failing — this backfills exactly those
/// rows, using the outgoing shared secret, so no device needs reflashing. Idempotent: only
/// device ids without a matching row are touched.
/// </summary>
public sealed class AttendanceDeviceSeeder
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly DeviceIngestOptions _options;
    private readonly ILogger<AttendanceDeviceSeeder> _logger;

    public AttendanceDeviceSeeder(
        AppDbContext db,
        IClock clock,
        IOptions<DeviceIngestOptions> options,
        ILogger<AttendanceDeviceSeeder> logger)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var knownKeys = await _db.AttendanceDevices
            .Select(device => device.DeviceKey)
            .ToListAsync(ct);

        var usedKeys = await _db.AttendanceLogs
            .Where(log => log.Source == AttendanceSource.Device && log.DeviceId != null)
            .Select(log => log.DeviceId!)
            .Distinct()
            .ToListAsync(ct);

        var missingKeys = usedKeys.Except(knownKeys).ToList();
        if (missingKeys.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.HmacSecret))
        {
            _logger.LogWarning(
                "{Count} device id(s) have punch history but no registry row, and no legacy " +
                "shared secret is configured to backfill them: {DeviceKeys}. They will reject " +
                "every request until registered via the device admin screen.",
                missingKeys.Count,
                string.Join(", ", missingKeys));
            return;
        }

        var now = _clock.GetCurrentInstant();
        foreach (var key in missingKeys)
        {
            _db.AttendanceDevices.Add(AttendanceDevice.Register(
                key,
                $"Legacy device {key}",
                _options.HmacSecret,
                registeredByUserId: null,
                now));
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Backfilled {Count} attendance device(s) from punch history: {DeviceKeys}.",
            missingKeys.Count,
            string.Join(", ", missingKeys));
    }
}
