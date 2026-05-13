namespace Erp.Infrastructure.Authentication;

public static class JwtSigningKeyHelper
{
    public static byte[] DecodeSigningKey(string signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);

        try
        {
            var bytes = Convert.FromBase64String(signingKey);
            if (bytes.Length < 32)
            {
                throw new InvalidOperationException(
                    $"JWT signing key must decode to at least 32 bytes (got {bytes.Length}).");
            }

            return bytes;
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "JWT signing key must be a valid Base64-encoded string.");
        }
    }
}
