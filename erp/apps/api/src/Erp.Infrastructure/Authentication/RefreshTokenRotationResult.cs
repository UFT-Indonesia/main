using Erp.Infrastructure.Identity;
using NodaTime;

namespace Erp.Infrastructure.Authentication;

public abstract record RefreshTokenRotationResult
{
    public sealed record Success(
        ApplicationUser User,
        string PlainTextToken,
        Instant ExpiresAtUtc) : RefreshTokenRotationResult;

    public sealed record Invalid(string Message) : RefreshTokenRotationResult;

    public sealed record Compromised(string Message) : RefreshTokenRotationResult;
}
