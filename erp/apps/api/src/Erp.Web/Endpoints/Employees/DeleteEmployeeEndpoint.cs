using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.DeleteEmployee;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Employees;

/// <summary>
/// Owner-only by design — unlike update (see <see cref="Accounts.AccountRules.CanManage"/>),
/// termination is never delegated to a Manager, so the blanket role gate is the whole rule.
/// </summary>
[Authorize(Roles = "Owner")]
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
        var result = await _bus.InvokeAsync<Result<EmployeeResult>>(
            new DeleteEmployeeCommand(req.Id, req.TerminationDate), ct);

        if (result is Result<EmployeeResult>.Success s)
        {
            await SendOkAsync(EmployeeResponseMapper.ToResponse(s.Value, User), ct);
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
