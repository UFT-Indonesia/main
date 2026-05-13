namespace Erp.Web.Endpoints.Auth;

public sealed class LoginRequest
{
    public string Email { get; init; } = default!;
    public string Password { get; init; } = default!;
}

public sealed class AuthResponse
{
    public string AccessToken { get; init; } = default!;
    public string TokenType { get; init; } = default!;
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public AuthUserResponse User { get; init; } = default!;
}

public sealed class AuthUserResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public Guid? EmployeeId { get; init; }
    public IList<string> Roles { get; init; } = default!;
}
