using Erp.UseCases.Common;
using Erp.UseCases.Probation.Common;

namespace Erp.UseCases.Probation.ListProbationExtensionRequests;

public sealed record ListProbationExtensionRequestsQuery(
    int Page,
    int PageSize,
    string? Status,
    Guid? EmployeeId,
    Caller Caller);

public sealed class ListProbationExtensionRequestsResult
{
    public IReadOnlyList<ProbationExtensionResult> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
