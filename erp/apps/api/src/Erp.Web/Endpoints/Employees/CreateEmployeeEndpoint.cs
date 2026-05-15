using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.CreateEmployee;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Employees;

[Authorize]
public sealed class CreateEmployeeEndpoint : Endpoint<CreateEmployeeRequest, EmployeeResponse>
{
    private readonly IMessageBus _bus;

    public CreateEmployeeEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Post("/");
        Group<EmployeeGroup>();
    }

    public override async Task HandleAsync(CreateEmployeeRequest req, CancellationToken ct)
    {
        // TODO: Enforce RBS permission check — only authorized roles should be able to create employees.
        var result = await _bus.InvokeAsync<Result<EmployeeResult>>(new CreateEmployeeCommand(
            req.FullName,
            req.Nik,
            req.Npwp,
            req.MonthlyWageAmount,
            req.EffectiveSalaryFrom,
            req.Role,
            req.ParentId), ct);

        if (result is Result<EmployeeResult>.Success s)
        {
            await SendCreatedAtAsync<CreateEmployeeEndpoint>(
                null,
                EmployeeResponseMapper.ToResponse(s.Value),
                cancellation: ct);
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
