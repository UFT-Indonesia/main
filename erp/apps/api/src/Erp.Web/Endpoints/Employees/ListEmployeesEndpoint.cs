using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.ListEmployees;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Employees;

/// <summary>
/// Open to every employee: pickers and the leave calendar need to put names to ids. What comes
/// back is stripped per row by the mapper — a colleague gets a name, a role and a status, while
/// national ID, tax ID and pay need standing (EmployeeVisibility).
/// </summary>
[Authorize]
public sealed class ListEmployeesEndpoint : Endpoint<ListEmployeesRequest, ListEmployeesResponse>
{
    private readonly IMessageBus _bus;

    public ListEmployeesEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/");
        Group<EmployeeGroup>();
    }

    public override async Task HandleAsync(ListEmployeesRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<ListEmployeesResult>>(new ListEmployeesQuery(
            req.Page,
            req.PageSize,
            req.Search,
            req.Role,
            req.Status), ct);

        if (result is Result<ListEmployeesResult>.Success s)
        {
            await SendOkAsync(new ListEmployeesResponse
            {
                Items = s.Value.Items.Select(item => EmployeeResponseMapper.ToResponse(item, caller)).ToList(),
                Page = s.Value.Page,
                PageSize = s.Value.PageSize,
                TotalCount = s.Value.TotalCount,
            }, ct);
            return;
        }

        if (result is Result<ListEmployeesResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        if (result is Result<ListEmployeesResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
