using Erp.Infrastructure.Identity;

namespace Erp.Web.Endpoints.Auth;

public sealed class LoginRequest
{
    public string Username { get; init; } = default!;
    public string Password { get; init; } = default!;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = default!;
    public string NewPassword { get; init; } = default!;
}

public sealed class AuthResponse
{
    public string AccessToken { get; init; } = default!;
    public string TokenType { get; init; } = default!;
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public DateTimeOffset RefreshTokenExpiresAtUtc { get; init; }
    public AuthUserResponse User { get; init; } = default!;
}

public sealed class AuthErrorResponse
{
    public string Message { get; init; } = default!;
}

public sealed class AuthUserResponse
{
    public Guid Id { get; init; }
    public string Username { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public Guid? EmployeeId { get; init; }
    public bool MustChangePassword { get; init; }
    public IList<string> Roles { get; init; } = default!;

    /// <summary>Roles stay an array for the client's sake, but an account now resolves to exactly one.</summary>
    public static AuthUserResponse From(ApplicationUser user, AccountIdentity identity) => new()
    {
        Id = user.Id,
        Username = user.UserName ?? string.Empty,
        Email = user.Email ?? string.Empty,
        FullName = user.FullName,
        EmployeeId = identity.EmployeeId,
        MustChangePassword = user.MustChangePassword,
        Roles = [identity.Role.ToString()],
    };
}
