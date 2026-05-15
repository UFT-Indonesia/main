using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.GetEmployeeById;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Employees;

[Authorize]
public sealed class GetEmployeeEndpoint : Endpoint<GetEmployeeByIdRequest, EmployeeResponse>
{
    private readonly IMessageBus _bus;

    public GetEmployeeEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/{Id:guid}");
        Group<EmployeeGroup>();
    }

    public override async Task HandleAsync(GetEmployeeByIdRequest req, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<EmployeeResult>>(
            new GetEmployeeByIdQuery(req.Id), ct);

        if (result is Result<EmployeeResult>.Success s)
        {
            await SendOkAsync(EmployeeResponseMapper.ToResponse(s.Value), ct);
            return;
        }

        if (result is Result<EmployeeResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<EmployeeResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }
}
