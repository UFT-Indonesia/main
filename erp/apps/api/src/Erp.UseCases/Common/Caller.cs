using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;

namespace Erp.UseCases.Common;

/// <summary>
/// Who is making the request, resolved from the access token. <see cref="EmployeeId"/> is
/// null for accounts with no employee record — they can still act on their role's authority
/// but can never be the subject of anything employee-scoped.
/// </summary>
public readonly record struct Caller(Guid UserId, EmployeeRole Role, EmployeeId? EmployeeId, string Name);

/// <summary>Error code handlers return when a caller is authenticated but not permitted; endpoints map it to 403.</summary>
public static class ResultErrors
{
    public const string Forbidden = "auth.forbidden";
}
