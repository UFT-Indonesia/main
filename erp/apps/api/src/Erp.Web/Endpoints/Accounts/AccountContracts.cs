using System.Security.Claims;
using Erp.UseCases.Common;
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

/// <summary>
/// Two different questions used to share one method, which is how a Manager ended up able to
/// edit Staff outside their own line: "may you act on this person?" is scoped to the reporting
/// line, while "may you hand out this role?" is about privilege alone.
/// </summary>
public static class AccountRules
{
    /// <summary>
    /// May the caller act on this specific person? An Owner may act on anyone. A Manager may
    /// act only on their own direct Staff — a Staff member with no manager assigned, or one in
    /// another line, is the Owner's to handle until the org chart says otherwise.
    /// </summary>
    public static bool CanManage(Caller caller, EmployeeRole targetRole, Guid? targetParentId) =>
        caller.Role switch
        {
            EmployeeRole.Owner => true,
            EmployeeRole.Manager => targetRole == EmployeeRole.Staff
                && caller.EmployeeId is { } callerEmployeeId
                && targetParentId == callerEmployeeId.Value,
            _ => false,
        };

    /// <summary>
    /// May the caller hand out this role at all? Deliberately line-agnostic — it guards
    /// privilege escalation (a Manager promoting someone to Owner), not who reports to whom.
    /// </summary>
    public static bool CanGrantRole(Caller caller, EmployeeRole role) =>
        caller.Role == EmployeeRole.Owner
        || (caller.Role == EmployeeRole.Manager && role == EmployeeRole.Staff);
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
