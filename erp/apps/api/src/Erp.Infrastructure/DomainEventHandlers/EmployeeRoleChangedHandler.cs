using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Interfaces;
using Erp.Infrastructure.Authentication;
using Erp.SharedKernel.Identity;
using Wolverine;

namespace Erp.Infrastructure.DomainEventHandlers;

public static class EmployeeRoleChangedHandler
{
    /// <summary>
    /// Revokes refresh tokens so the old role's access token cannot be renewed. The role
    /// itself is no longer copied onto the Identity account — <c>Employee.Role</c> is the
    /// single source of truth and is read fresh when the next token is issued. Also audited:
    /// a promotion changes what someone can see, so the Owner needs the history.
    /// </summary>
    public static async Task Handle(
        EmployeeRoleChanged message,
        IRefreshTokenService refreshTokenService,
        IRepository<EmployeeAuditLog> auditLogs,
        Envelope envelope,
        CancellationToken ct)
    {
        await refreshTokenService.RevokeAllForEmployeeAsync(message.EmployeeId, "employee_role_changed", ct);

        await EmployeeAuditLogWriter.WriteAsync(
            auditLogs,
            envelope,
            new EmployeeId(message.EmployeeId),
            message.EventType,
            message.RaisedAt,
            oldValue: new RoleAuditValue(message.OldRole.ToString()),
            newValue: new RoleAuditValue(message.NewRole.ToString()),
            ct);
    }
}
