using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common.Filtering;

namespace Erp.Web.Endpoints;

/// <summary>
/// Turns the raw <c>filter=</c> query parameter into rows. Malformed input throws a
/// <see cref="DomainException"/>, which the handler already renders as a 400 with the error
/// code — so every list endpoint gets identical behaviour from one line.
/// </summary>
public static class FilterBinding
{
    public static IReadOnlyList<FilterRow> ParseOrThrow(string? filter) => FilterApplier.Parse(filter) switch
    {
        Result<IReadOnlyList<FilterRow>>.Success success => success.Value,
        Result<IReadOnlyList<FilterRow>>.Error error => throw new DomainException(error.Code, error.Message),
        var other => throw new InvalidOperationException($"Unexpected result type: {other.GetType().Name}"),
    };
}
