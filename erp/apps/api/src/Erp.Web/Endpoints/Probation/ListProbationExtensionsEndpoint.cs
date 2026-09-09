using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Probation.ListProbationExtensionRequests;
using Erp.UseCases.Common.Filtering;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Probation;

/// <summary>
/// Not a company-wide list: an owner sees every request, a Manager only their own direct Staff's,
/// and everyone else an empty page. Scope is applied to the query, so paging stays honest.
/// </summary>
[Authorize(Roles = "Owner,Manager")]
public sealed class ListProbationExtensionsEndpoint
    : Endpoint<ListProbationExtensionsRequest, ListProbationExtensionsResponse>
{
    private readonly IMessageBus _bus;

    public ListProbationExtensionsEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/");
        Group<ProbationGroup>();
    }

    public override async Task HandleAsync(ListProbationExtensionsRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<ListProbationExtensionRequestsResult>>(
            new ListProbationExtensionRequestsQuery(
                req.Page, req.PageSize, FilterBinding.ParseOrThrow(req.Filter), caller), ct);

        if (result is Result<ListProbationExtensionRequestsResult>.Success s)
        {
            await SendOkAsync(new ListProbationExtensionsResponse
            {
                Items = s.Value.Items.Select(ProbationExtensionResponse.From).ToList(),
                Page = s.Value.Page,
                PageSize = s.Value.PageSize,
                TotalCount = s.Value.TotalCount,
            }, ct);
            return;
        }

        if (result is Result<ListProbationExtensionRequestsResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<ListProbationExtensionRequestsResult>.Error e)
        {
            if (e.Code == FilterErrors.FieldForbidden)
            {
                await SendForbiddenAsync(ct);
                return;
            }

            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
