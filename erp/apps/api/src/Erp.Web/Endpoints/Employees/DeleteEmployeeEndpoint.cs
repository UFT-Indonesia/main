using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.DeleteEmployee;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Employees;

[Authorize]
public sealed class DeleteEmployeeEndpoint : Endpoint<DeleteEmployeeRouteRequest, EmployeeResponse>
{
    private readonly IMessageBus _bus;

    public DeleteEmployeeEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Delete("/{Id:guid}");
        Group<EmployeeGroup>();
    }

    public override async Task HandleAsync(DeleteEmployeeRouteRequest req, CancellationToken ct)
    {
        // TODO: Enforce RBS permission check — only authorized roles should be able to terminate employees.
        var result = await _bus.InvokeAsync<Result<EmployeeResult>>(
            new DeleteEmployeeCommand(req.Id, req.TerminationDate), ct);

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
