using Erp.Core.Aggregates.Employees.Events;
using Erp.Infrastructure.Authentication;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeRoleChangedHandler
{
    /// <summary>
    /// Revokes refresh tokens so the old role's access token cannot be renewed. The role
    /// itself is no longer copied onto the Identity account — <c>Employee.Role</c> is the
    /// single source of truth and is read fresh when the next token is issued.
    /// </summary>
    public static Task Handle(
        EmployeeRoleChanged message,
        IRefreshTokenService refreshTokenService,
        CancellationToken ct) =>
        refreshTokenService.RevokeAllForEmployeeAsync(message.EmployeeId, "employee_role_changed", ct);
}
