namespace Erp.UseCases.Employees.Common;

/// <summary>
/// Envelope headers carrying "who did this" from the request-scoped use-case handler to the
/// audit-log handler. The audit handler runs on a background queue after the HTTP response
/// has completed, so it cannot read the caller itself — <c>HttpContext</c> is already gone
/// by then. Headers travel with the persisted envelope, so this survives durable queues and
/// restarts too.
/// </summary>
public static class EmployeeAuditHeaders
{
    public const string ActorUserId = "erp.actor.user-id";

    public const string ActorName = "erp.actor.name";
}
