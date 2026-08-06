using System.Security.Claims;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.CreateLeaveRequest;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

/// <summary>
/// Scoped by <see cref="LeaveRules.CanFileFor"/>: Staff file for themselves, a Manager for
/// themselves or their own Staff, an Owner for anyone. An Owner's own leave is approved on
/// creation — nobody outranks them to decide it.
/// </summary>
[Authorize]
public sealed class CreateLeaveRequestEndpoint : Endpoint<CreateLeaveRequestRequest, LeaveRequestResponse>
{
    private readonly IMessageBus _bus;

    public CreateLeaveRequestEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Post("/");
        Group<LeaveGroup>();
    }

    public override async Task HandleAsync(CreateLeaveRequestRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<LeaveRequestResult>>(new CreateLeaveRequestCommand(
            req.EmployeeId,
            req.Type,
            req.StartDate,
            req.EndDate,
            req.Reason,
            caller), ct);

        if (result is Result<LeaveRequestResult>.Success s)
        {
            await SendCreatedAtAsync<CreateLeaveRequestEndpoint>(
                null, LeaveRequestResponse.From(s.Value), cancellation: ct);
            return;
        }

        if (result is Result<LeaveRequestResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<LeaveRequestResult>.Error e)
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
