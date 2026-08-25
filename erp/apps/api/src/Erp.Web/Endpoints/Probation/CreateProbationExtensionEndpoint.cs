using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Probation.Common;
using Erp.UseCases.Probation.CreateProbationExtensionRequest;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Probation;

/// <summary>
/// A Manager asking an owner for more probation time for one of their own direct Staff. Owners
/// are excluded by <see cref="ProbationRules.CanFileFor"/> rather than by the role gate — they
/// edit the date directly instead of asking themselves for permission.
/// </summary>
[Authorize(Roles = "Manager")]
public sealed class CreateProbationExtensionEndpoint
    : Endpoint<CreateProbationExtensionRequest, ProbationExtensionResponse>
{
    private readonly IMessageBus _bus;

    public CreateProbationExtensionEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Post("/");
        Group<ProbationGroup>();
    }

    public override async Task HandleAsync(CreateProbationExtensionRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<ProbationExtensionResult>>(
            new CreateProbationExtensionRequestCommand(req.EmployeeId, req.ProposedEndsOn, req.Reason, caller), ct);

        if (result is Result<ProbationExtensionResult>.Success s)
        {
            await SendCreatedAtAsync<CreateProbationExtensionEndpoint>(
                null, ProbationExtensionResponse.From(s.Value), cancellation: ct);
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
