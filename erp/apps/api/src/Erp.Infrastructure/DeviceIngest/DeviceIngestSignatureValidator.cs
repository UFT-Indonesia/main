using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Erp.Infrastructure.DeviceIngest;

public sealed class DeviceIngestSignatureValidator : IDeviceIngestSignatureValidator
{
    private readonly DeviceIngestOptions _options;
    private readonly IClock _clock;

    public DeviceIngestSignatureValidator(IOptions<DeviceIngestOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public DeviceIngestSignatureResult Validate(string payload, string? timestamp, string? signature)
    {
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

        var expectedSignature = ComputeSignature(payload, timestamp, _options.HmacSecret);
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
