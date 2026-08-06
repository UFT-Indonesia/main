using System.Security.Cryptography;

namespace Erp.UseCases.Attendance.Devices.Common;

internal static class DeviceSecretGenerator
{
    private const int ByteLength = 32;

    /// <summary>256 bits of randomness, base64url-encoded — an HMAC key, not a human-typed password, so no character-class rules apply.</summary>
    internal static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(ByteLength);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
