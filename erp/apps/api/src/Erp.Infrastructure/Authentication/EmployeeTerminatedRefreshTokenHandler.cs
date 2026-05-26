using Erp.Core.Aggregates.Employees.Events;

namespace Erp.Infrastructure.Authentication;

public static class EmployeeTerminatedRefreshTokenHandler
{
    public static Task Handle(
        EmployeeTerminated message,
        IRefreshTokenService refreshTokenService,
        CancellationToken ct)
    {
        return refreshTokenService.RevokeAllForEmployeeAsync(message.EmployeeId, "employee_terminated", ct);
    }
}
