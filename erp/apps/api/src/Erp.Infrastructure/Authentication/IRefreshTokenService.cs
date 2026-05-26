using Erp.Infrastructure.Identity;

namespace Erp.Infrastructure.Authentication;

public interface IRefreshTokenService
{
    Task<RefreshTokenIssueResult> IssueAsync(
        ApplicationUser user,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct);

    Task<RefreshTokenRotationResult> RotateAsync(
        string plainTextToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct);

    Task RevokeFamilyForTokenAsync(string plainTextToken, string reason, CancellationToken ct);

    Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct);

    Task RevokeAllForEmployeeAsync(Guid employeeId, string reason, CancellationToken ct);
}
