namespace Erp.Web.Endpoints.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc, AuthUserResponse User);

public sealed record AuthUserResponse(Guid Id, string Email, string FullName, Guid? EmployeeId, IList<string> Roles);
