using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Erp.Infrastructure.DeviceIngest;

public sealed class DeviceIngestSignatureValidator : IDeviceIngestSignatureValidator
{
    private readonly DeviceIngestOptions _options;
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public DeviceIngestSignatureValidator(IOptions<DeviceIngestOptions> options, AppDbContext db, IClock clock)
    {
        _options = options.Value;
        _db = db;
        _clock = clock;
    }

    public async Task<DeviceIngestSignatureResult> ValidateAsync(
        string payload,
        string? deviceKey,
        string? timestamp,
        string? signature,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
        {
            return DeviceIngestSignatureResult.Invalid("device_ingest.device_key_required");
        }

        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return DeviceIngestSignatureResult.Invalid("device_ingest.timestamp_required");
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            return DeviceIngestSignatureResult.Invalid("device_ingest.signature_required");
        }

        timestamp = timestamp.Trim();
        signature = signature.Trim();

        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return DeviceIngestSignatureResult.Invalid("device_ingest.timestamp_invalid");
        }

        var signedAt = Instant.FromUnixTimeSeconds(unixSeconds);
        var age = _clock.GetCurrentInstant() - signedAt;
        if (age < Duration.Zero)
        {
            age = -age;
        }

        if (age > Duration.FromSeconds(_options.ToleranceSeconds))
        {
            return DeviceIngestSignatureResult.Invalid("device_ingest.timestamp_out_of_tolerance");
        }

        // Checked after the timestamp, not before: a device that fails on tolerance shouldn't
        // also leak whether its id happens to be registered.
        var device = await _db.AttendanceDevices.AsNoTracking()
            .SingleOrDefaultAsync(d => d.DeviceKey == deviceKey.Trim(), ct);
        if (device is null)
        {
            return DeviceIngestSignatureResult.Invalid("device_ingest.device_unknown");
        }

        if (!device.Enabled)
        {
            return DeviceIngestSignatureResult.Invalid("device_ingest.device_disabled");
        }

        var expectedSignature = ComputeSignature(payload, timestamp, device.Secret);
        var normalizedSignature = signature.Trim().ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(normalizedSignature)))
        {
            return DeviceIngestSignatureResult.Invalid("device_ingest.signature_invalid");
        }

        return DeviceIngestSignatureResult.Valid;
    }

    public static string ComputeSignature(string payload, string timestamp, string secret)
    {
        var signingPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingPayload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
