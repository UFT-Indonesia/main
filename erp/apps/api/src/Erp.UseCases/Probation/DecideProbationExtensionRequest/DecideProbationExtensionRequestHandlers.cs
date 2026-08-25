using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Probation;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Probation.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Probation.DecideProbationExtensionRequest;

// Lifecycle violations (already decided) throw DomainException from the aggregate and bubble to
// the global exception handler as 400s, matching the leave decision handlers.

public static class ApproveProbationExtensionHandler
{
    public static Task<Result<ProbationExtensionResult>> Handle(
        ApproveProbationExtensionCommand command,
        IRepository<ProbationExtensionRequest> requests,
        IRepository<Employee> employees,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct) =>
        DecideProbationExtensionService.DecideAsync(
            command.RequestId,
            command.Caller,
            requiresOwner: true,
            requiresActiveProbation: true,
            // Approving writes the exact date that was agreed to, rather than re-deriving one now.
            (request, subject, now) =>
            {
                request.Approve(command.Caller.UserId, command.Caller.Name, now, command.Note);
                subject.OverrideProbationEnd(request.ProposedEndsOn);
            },
            requests,
            employees,
            clock,
            bus,
            ct);
}

public static class DenyProbationExtensionHandler
{
    public static Task<Result<ProbationExtensionResult>> Handle(
        DenyProbationExtensionCommand command,
        IRepository<ProbationExtensionRequest> requests,
        IRepository<Employee> employees,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct) =>
        DecideProbationExtensionService.DecideAsync(
            command.RequestId,
            command.Caller,
            requiresOwner: true,
            requiresActiveProbation: false,
            (request, _, now) => request.Deny(command.Caller.UserId, command.Caller.Name, now, command.Note),
            requests,
            employees,
            clock,
            bus,
            ct);
}

public static class CancelProbationExtensionHandler
{
    public static Task<Result<ProbationExtensionResult>> Handle(
        CancelProbationExtensionCommand command,
        IRepository<ProbationExtensionRequest> requests,
        IRepository<Employee> employees,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct) =>
        DecideProbationExtensionService.DecideAsync(
            command.RequestId,
            command.Caller,
            requiresOwner: false,
            requiresActiveProbation: false,
            (request, _, now) => request.Cancel(command.Caller.UserId, command.Caller.Name, now, command.Note),
            requests,
            employees,
            clock,
            bus,
            ct);
}

internal static class DecideProbationExtensionService
{
    internal static async Task<Result<ProbationExtensionResult>> DecideAsync(
        Guid requestId,
        Caller caller,
        bool requiresOwner,
        bool requiresActiveProbation,
        Action<ProbationExtensionRequest, Employee, Instant> decide,
        IRepository<ProbationExtensionRequest> requests,
        IRepository<Employee> employees,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct)
    {
        var request = await requests.FirstOrDefaultAsync(
            new ProbationExtensionByIdSpec(new ProbationExtensionRequestId(requestId)), ct);
        if (request is null)
        {
            return new Result<ProbationExtensionResult>.NotFound("Probation extension request was not found.");
        }

        var subject = await employees.GetByIdAsync(request.EmployeeId, ct);
        if (subject is null)
        {
            return new Result<ProbationExtensionResult>.NotFound(
                "The employee this request belongs to was not found.");
        }

        var permitted = requiresOwner
            ? ProbationRules.CanDecide(caller)
            : ProbationRules.CanCancel(caller, request.RequestedByUserId);

        if (!permitted)
        {
            return new Result<ProbationExtensionResult>.Error(
                ResultErrors.Forbidden, "You cannot decide this probation extension request.");
        }

        // Probation can lapse while a request waits for a decision. Approving then would
        // retroactively un-confirm someone whose annual leave may already have been approved in
        // the gap, so the Owner is sent to the direct edit instead. Denying a stale request is
        // still allowed — that just closes it out.
        if (requiresActiveProbation && !subject.IsOnProbation(DisplayZone.Today(clock)))
        {
            return new Result<ProbationExtensionResult>.Error(
                "probation.already_confirmed",
                $"{subject.FullName}'s probation has already ended. "
                + "Edit the probation end date directly if it still needs to move.");
        }

        decide(request, subject, clock.GetCurrentInstant());

        await requests.UpdateAsync(request, ct);
        if (subject.DomainEvents.Count > 0)
        {
            await employees.UpdateAsync(subject, ct);
            await EmployeeDomainEventPublisher.PublishAsync(subject.DomainEvents, bus, caller);
        }

        return new Result<ProbationExtensionResult>.Success(
            ProbationExtensionResult.From(request, caller, subject.FullName));
    }
}
