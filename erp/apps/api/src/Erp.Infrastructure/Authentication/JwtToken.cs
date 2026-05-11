namespace Erp.Infrastructure.Authentication;

public sealed record JwtToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
