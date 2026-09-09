using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Probation;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common.Filtering;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Probation.Common;

namespace Erp.UseCases.Probation.ListProbationExtensionRequests;

/// <summary>
/// Unlike the leave list, this is not a company-wide calendar — "should this person's probation
/// be extended" is a conversation between a Manager and an Owner. An Owner sees every request; a
/// Manager sees only their own direct Staff's; nobody else sees any. Scope is applied to the
/// query rather than by blanking rows, so a page of 20 is 20 visible rows.
/// </summary>
public static class ListProbationExtensionRequestsHandler
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static async Task<Result<ListProbationExtensionRequestsResult>> Handle(
        ListProbationExtensionRequestsQuery query,
        IReadRepository<ProbationExtensionRequest> requests,
        IReadRepository<Employee> employees,
        CancellationToken ct)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

        if (!FilterApplier.TryCompile(ProbationExtensionFilterFields.Fields, query.Filters, query.Caller, out var filters, out var failure))
        {
            return new Result<ListProbationExtensionRequestsResult>.Error(failure.Code, failure.Message);
        }

        var scope = await ResolveScopeAsync(query, employees, ct);

        var totalCount = await requests.CountAsync(
            new ProbationExtensionCountSpec(filters, scope), ct);
        var items = await requests.ListAsync(
            new ProbationExtensionListSpec(page, pageSize, filters, scope), ct);

        return new Result<ListProbationExtensionRequestsResult>.Success(
            new ListProbationExtensionRequestsResult
            {
                Items = items.Select(request => ProbationExtensionResult.From(request, query.Caller)).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            });
    }

    /// <summary>
    /// The employees whose requests this caller may see. Null means no restriction (an Owner);
    /// an empty set means nothing at all. Any employee the user actually asked for arrives as an
    /// ordinary filter row and is ANDed on top, so scope stays purely about authority.
    /// </summary>
    private static async Task<IReadOnlyCollection<EmployeeId>?> ResolveScopeAsync(
        ListProbationExtensionRequestsQuery query,
        IReadRepository<Employee> employees,
        CancellationToken ct)
    {
        if (query.Caller.Role == EmployeeRole.Owner)
        {
            return null;
        }

        if (query.Caller.Role != EmployeeRole.Manager || query.Caller.EmployeeId is not { } managerId)
        {
            return [];
        }

        var reports = await employees.ListAsync(new ActiveDirectReportsSpec(managerId), ct);
        return reports.Select(employee => employee.Id).ToList();
    }
}
