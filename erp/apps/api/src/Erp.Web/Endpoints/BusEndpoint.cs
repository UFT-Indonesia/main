using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints;

/// <summary>
/// The plumbing every overtime/payroll endpoint repeats: resolve the caller, send one message,
/// map the <see cref="Result{T}"/> to a status. Authority lives in the handler, so the gate here
/// is only "must be signed in" — same as leave.
/// </summary>
[Authorize]
public abstract class BusEndpoint<TReq, TValue> : Endpoint<TReq, TValue>
    where TReq : notnull
    where TValue : notnull
{
    private readonly IMessageBus _bus;

    protected BusEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    protected abstract object Build(TReq req, Caller caller);

    public override async Task HandleAsync(TReq req, CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendResultAsync(await _bus.InvokeAsync<Result<TValue>>(Build(req, caller), ct), ct);
    }

    protected async Task SendResultAsync(Result<TValue> result, CancellationToken ct)
    {
        switch (result)
        {
            case Result<TValue>.Success s:
                await SendOkAsync(s.Value, ct);
                return;
            case Result<TValue>.NotFound:
                await SendNotFoundAsync(ct);
                return;
            case Result<TValue>.Error { Code: ResultErrors.Forbidden }:
                await SendForbiddenAsync(ct);
                return;
            case Result<TValue>.Error e:
                throw new DomainException(e.Code, e.Message);
            default:
                throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
        }
    }
}
