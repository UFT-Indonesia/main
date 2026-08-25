using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Probation.Common;
using Erp.UseCases.Probation.DecideProbationExtensionRequest;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Probation;

/// <summary>
/// Shared plumbing for approve/deny/cancel, mirroring the leave decision endpoints. Authority is
/// enforced per-request by <see cref="ProbationRules"/> — approve and deny take an owner, cancel
/// takes the Manager who filed it — so the role gate here is only "must be signed in".
/// </summary>
[Authorize]
public abstract class DecideProbationExtensionEndpointBase
    : Endpoint<DecideProbationExtensionRequest, ProbationExtensionResponse>
{
    private readonly IMessageBus _bus;

    protected DecideProbationExtensionEndpointBase(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Route segment under /api/probation/{id:guid}/ (e.g. "approve").</summary>
    protected abstract string Action { get; }

    protected abstract object BuildCommand(DecideProbationExtensionRequest req, Caller caller);

    public override void Configure()
    {
        Post($"/{{id:guid}}/{Action}");
        Group<ProbationGroup>();
    }

    public override async Task HandleAsync(DecideProbationExtensionRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<ProbationExtensionResult>>(BuildCommand(req, caller), ct);

        if (result is Result<ProbationExtensionResult>.Success s)
        {
            await SendOkAsync(ProbationExtensionResponse.From(s.Value), ct);
            return;
        }

        if (result is Result<ProbationExtensionResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<ProbationExtensionResult>.Error e)
        {
            if (e.Code == ResultErrors.Forbidden)
            {
                await SendForbiddenAsync(ct);
                return;
            }

            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}

public sealed class ApproveProbationExtensionEndpoint : DecideProbationExtensionEndpointBase
{
    public ApproveProbationExtensionEndpoint(IMessageBus bus) : base(bus) { }

    protected override string Action => "approve";

    protected override object BuildCommand(DecideProbationExtensionRequest req, Caller caller) =>
        new ApproveProbationExtensionCommand(req.Id, caller, req.Note);
}

public sealed class DenyProbationExtensionEndpoint : DecideProbationExtensionEndpointBase
{
    public DenyProbationExtensionEndpoint(IMessageBus bus) : base(bus) { }

    protected override string Action => "deny";

    protected override object BuildCommand(DecideProbationExtensionRequest req, Caller caller) =>
        new DenyProbationExtensionCommand(req.Id, caller, req.Note);
}

public sealed class CancelProbationExtensionEndpoint : DecideProbationExtensionEndpointBase
{
    public CancelProbationExtensionEndpoint(IMessageBus bus) : base(bus) { }

    protected override string Action => "cancel";

    protected override object BuildCommand(DecideProbationExtensionRequest req, Caller caller) =>
        new CancelProbationExtensionCommand(req.Id, caller, req.Note);
}
