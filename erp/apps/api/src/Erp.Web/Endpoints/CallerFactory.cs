using System.Security.Claims;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Identity;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;

namespace Erp.Web.Endpoints;

public static class CallerFactory
{
    /// <summary>
    /// Reads the caller straight off the token. Role and employee id are stamped at issue
    /// time from the employee record, so nothing here needs to touch the database — but
    /// anything that can change mid-token (ParentId) is deliberately left out.
    /// </summary>
    public static Caller? From(ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return null;
        }

        var role = EmployeeRoleNames.MostPrivileged(
            principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value));

        var employeeId = Guid.TryParse(principal.FindFirstValue(JwtTokenService.EmployeeIdClaim), out var parsed)
            ? new EmployeeId(parsed)
            : (EmployeeId?)null;

        return new Caller(userId, role, employeeId, principal.FindFirstValue(ClaimTypes.Name) ?? "—");
    }
}
