using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Leave.Events;
using Wolverine;

namespace Erp.UseCases.Leave.Common;

/// <summary>
/// Publishes what a decision raised. Wolverine routes on the message's static type, so each
/// event is matched to its own concrete type rather than sent as <c>IDomainEvent</c> — which
/// would find no handler at all.
/// </summary>
internal static class LeaveRequestEventPublisher
{
    internal static async Task PublishAsync(LeaveRequest request, IMessageBus bus)
    {
        foreach (var domainEvent in request.DomainEvents)
        {
            switch (domainEvent)
            {
                case LeaveRequestApproved approved:
                    await bus.PublishAsync(approved);
                    break;
                case LeaveRequestCancelled cancelled:
                    await bus.PublishAsync(cancelled);
                    break;
            }
        }

        request.ClearDomainEvents();
    }
}
