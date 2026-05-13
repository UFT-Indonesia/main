using FastEndpoints;

namespace Erp.Web.Endpoints;

public sealed class HealthResponse
{
    public string Status { get; init; } = default!;
}

public sealed class HealthEndpoint : EndpointWithoutRequest<HealthResponse>
{
    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        return SendOkAsync(new HealthResponse { Status = "ok" }, ct);
    }
}
