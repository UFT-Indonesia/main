using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.UpdateEmployee;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Employees;

[Authorize]
public sealed class UpdateEmployeeEndpoint : Endpoint<UpdateEmployeeRouteRequest, EmployeeResponse>
{
    private readonly IMessageBus _bus;

    public UpdateEmployeeEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Put("/{Id:guid}");
        Group<EmployeeGroup>();
    }

    public override async Task HandleAsync(UpdateEmployeeRouteRequest req, CancellationToken ct)
    {
        // TODO: Enforce RBS permission check — only authorized roles should be able to update employees.
        var result = await _bus.InvokeAsync<Result<EmployeeResult>>(new UpdateEmployeeCommand(
            req.Id,
            req.FullName,
            req.Npwp,
            req.MonthlyWageAmount,
            req.EffectiveSalaryFrom,
            req.Role,
            req.ParentId), ct);

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
