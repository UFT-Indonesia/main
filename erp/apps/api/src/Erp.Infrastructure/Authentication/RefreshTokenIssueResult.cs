using NodaTime;

namespace Erp.Infrastructure.Authentication;

public sealed record RefreshTokenIssueResult(string PlainTextToken, Instant ExpiresAtUtc);
