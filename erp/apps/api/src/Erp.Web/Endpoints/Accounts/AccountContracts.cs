using System.Security.Claims;
using System.Security.Cryptography;
using Erp.Core.Aggregates.Employees;

namespace Erp.Web.Endpoints.Accounts;

public sealed class CreateAccountRequest
{
    public Guid EmployeeId { get; init; }
    public string Username { get; init; } = default!;
    public string? Email { get; init; }
}

public sealed class AccountResponse
{
    public Guid Id { get; init; }
    public string Username { get; init; } = default!;
    public string? Email { get; init; }
    public string FullName { get; init; } = default!;
    public Guid? EmployeeId { get; init; }
    public string Role { get; init; } = default!;
    public bool IsEnabled { get; init; }
    public bool MustChangePassword { get; init; }
}

public sealed class ListAccountsResponse
{
    public IReadOnlyList<AccountResponse> Items { get; init; } = default!;
}

public sealed class CreateAccountResponse
{
    public AccountResponse Account { get; init; } = default!;
    /// <summary>Shown exactly once; not retrievable afterwards.</summary>
    public string TempPassword { get; init; } = default!;
}

public sealed class SetAccountEnabledRequest
{
    public Guid Id { get; init; }
    public bool Enabled { get; init; }
}

public sealed class AccountIdRequest
{
    public Guid Id { get; init; }
}

public sealed class ResetAccountPasswordResponse
{
    /// <summary>Shown exactly once; not retrievable afterwards.</summary>
    public string TempPassword { get; init; } = default!;
}

public static class AccountRules
{
    /// <summary>Owner manages any account; Manager only Staff-role accounts.</summary>
    public static bool CanManage(ClaimsPrincipal caller, EmployeeRole targetRole) =>
        caller.IsInRole(nameof(EmployeeRole.Owner))
        || (caller.IsInRole(nameof(EmployeeRole.Manager)) && targetRole == EmployeeRole.Staff);

    /// <summary>Most privileged role wins when a user has multiple Identity roles (Owner &lt; Manager &lt; Staff by enum value).</summary>
    public static EmployeeRole RoleFromNames(IList<string> roles) =>
        roles
            .Select(r => Enum.TryParse<EmployeeRole>(r, out var role) ? role : (EmployeeRole?)null)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .DefaultIfEmpty(EmployeeRole.Staff)
            .Min();
}

public static class TempPassword
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    /// <summary>Random 12-char password satisfying the Identity policy (upper, lower, digit).</summary>
    public static string Generate()
    {
        while (true)
        {
            var chars = new char[12];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }

            var candidate = new string(chars);
            if (candidate.Any(char.IsUpper) && candidate.Any(char.IsLower) && candidate.Any(char.IsDigit))
            {
                return candidate;
            }
        }
    }
}
