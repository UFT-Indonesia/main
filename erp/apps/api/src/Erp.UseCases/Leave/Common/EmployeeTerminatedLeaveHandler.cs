using Ardalis.Specification;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Leave.Common;

public static class EmployeeTerminatedLeaveHandler
{
    /// <summary>
    /// Cancels whatever the terminated employee still had awaiting a decision. Approved
    /// leave is left alone — it already happened, and the record should stay truthful.
    /// </summary>
    public static async Task Handle(
        EmployeeTerminated message,
        IRepository<LeaveRequest> leaveRequests,
        IClock clock,
        CancellationToken ct)
    {
        var pending = await leaveRequests.ListAsync(
            new TrackedPendingLeaveForEmployeeSpec(new EmployeeId(message.EmployeeId)), ct);
        if (pending.Count == 0)
        {
            return;
        }

        var now = clock.GetCurrentInstant();
        foreach (var request in pending)
        {
            request.CancelForTermination(now);
            await leaveRequests.UpdateAsync(request, ct);
        }
    }
}

/// <summary>Like <see cref="PendingLeaveForEmployeeSpec"/>, but tracked so the rows can be mutated.</summary>
internal sealed class TrackedPendingLeaveForEmployeeSpec : Specification<LeaveRequest>
{
    public TrackedPendingLeaveForEmployeeSpec(EmployeeId employeeId)
    {
        Query.Where(request => request.EmployeeId == employeeId
                               && request.Status == LeaveRequestStatus.Pending);
    }
}
