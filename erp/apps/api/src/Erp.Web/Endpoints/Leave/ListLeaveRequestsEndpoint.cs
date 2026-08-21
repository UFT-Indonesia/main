using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Leave.ListLeaveRequests;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

/// <summary>
/// The company's leave calendar: every employee sees every row, so anyone can tell whether
/// the colleague — or the Owner — they need is away. Authority is applied per row rather than
/// per query: type, reason, decision note and the yearly balance are stripped from rows the
/// caller has no standing to read (LeaveRules.CanReadDetails / CanReadBalance). Accounts with
/// no employee record are not part of that audience and see nothing.
/// </summary>
[Authorize]
public sealed class ListLeaveRequestsEndpoint : Endpoint<ListLeaveRequestsRequest, ListLeaveRequestsResponse>
{
    private readonly IMessageBus _bus;

    public ListLeaveRequestsEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/");
        Group<LeaveGroup>();
    }

    public override async Task HandleAsync(ListLeaveRequestsRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<ListLeaveRequestsResult>>(new ListLeaveRequestsQuery(
            req.Page,
            req.PageSize,
            req.Status,
            req.EmployeeId,
            caller), ct);

        if (result is Result<ListLeaveRequestsResult>.Success s)
        {
            await SendOkAsync(new ListLeaveRequestsResponse
            {
                Items = s.Value.Items.Select(LeaveRequestResponse.From).ToList(),
                Page = s.Value.Page,
                PageSize = s.Value.PageSize,
                TotalCount = s.Value.TotalCount,
            }, ct);
            return;
        }

        if (result is Result<ListLeaveRequestsResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        if (result is Result<ListLeaveRequestsResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
