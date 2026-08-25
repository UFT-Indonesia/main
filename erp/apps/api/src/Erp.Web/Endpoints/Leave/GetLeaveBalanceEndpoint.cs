using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.GetLeaveBalance;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

/// <summary>
/// One employee's entitlement, usage and remaining days across all four types, for the year.
/// Scoped by <see cref="LeaveRules.CanReadBalance"/>: an owner reads anyone's, a Manager any
/// non-owner's so they can plan cover, and everyone else only their own.
/// </summary>
[Authorize]
public sealed class GetLeaveBalanceEndpoint : Endpoint<GetLeaveBalanceRequest, LeaveBalanceResponse>
{
    private readonly IMessageBus _bus;

    public GetLeaveBalanceEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/balance");
        Group<LeaveGroup>();
    }

    public override async Task HandleAsync(GetLeaveBalanceRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<LeaveBalanceResult>>(
            new GetLeaveBalanceQuery(req.EmployeeId, req.Year, caller), ct);

        if (result is Result<LeaveBalanceResult>.Success s)
        {
            await SendOkAsync(LeaveBalanceResponse.From(s.Value), ct);
            return;
        }

        if (result is Result<LeaveBalanceResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<LeaveBalanceResult>.Error e)
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
