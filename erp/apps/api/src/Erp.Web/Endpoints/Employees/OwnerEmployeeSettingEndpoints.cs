using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.SetLeaveQuota;
using Erp.UseCases.Employees.SetProbationEnd;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Employees;

/// <summary>
/// Shared plumbing for the owner-only employee settings that hang off
/// <c>/api/employees/{id}</c>. Both return the updated employee, so the client refreshes from
/// one response rather than re-fetching.
/// </summary>
[Authorize(Roles = "Owner")]
public abstract class OwnerEmployeeSettingEndpointBase<TRequest> : Endpoint<TRequest, EmployeeResponse>
    where TRequest : notnull
{
    private readonly IMessageBus _bus;

    protected OwnerEmployeeSettingEndpointBase(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Route segment under /api/employees/{id}/ (e.g. "probation").</summary>
    protected abstract string Segment { get; }

    protected abstract object BuildCommand(TRequest req, Caller caller);

    public override void Configure()
    {
        Put($"/{{Id:guid}}/{Segment}");
        Group<EmployeeGroup>();
    }

    public override async Task HandleAsync(TRequest req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<EmployeeResult>>(BuildCommand(req, caller), ct);

        if (result is Result<EmployeeResult>.Success s)
        {
            await SendOkAsync(EmployeeResponseMapper.ToResponse(s.Value, caller), ct);
            return;
        }

        if (result is Result<EmployeeResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<EmployeeResult>.Error e)
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

/// <summary>
/// The owner's direct edit of a probation end date. A Manager who wants more time files a
/// request against <c>/api/probation</c> instead, which an owner then approves.
/// </summary>
public sealed class SetProbationEndEndpoint : OwnerEmployeeSettingEndpointBase<SetProbationEndRouteRequest>
{
    public SetProbationEndEndpoint(IMessageBus bus) : base(bus) { }

    protected override string Segment => "probation";

    protected override object BuildCommand(SetProbationEndRouteRequest req, Caller caller) =>
        new SetProbationEndCommand(req.Id, req.EndsOn, caller);
}

/// <summary>
/// Per-employee leave entitlement override, one type per call. Null days clears the override
/// and returns that type to its default.
/// </summary>
public sealed class SetLeaveQuotaEndpoint : OwnerEmployeeSettingEndpointBase<SetLeaveQuotaRouteRequest>
{
    public SetLeaveQuotaEndpoint(IMessageBus bus) : base(bus) { }

    protected override string Segment => "quota";

    protected override object BuildCommand(SetLeaveQuotaRouteRequest req, Caller caller) =>
        new SetLeaveQuotaCommand(req.Id, req.Type, req.Days, caller);
}
