using System.Linq.Expressions;
using Ardalis.Specification;
using Erp.Core.Aggregates.Probation;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Probation.Common;

/// <summary>The employee's open extension request, if any — at most one Pending at a time.</summary>
internal sealed class PendingProbationExtensionForEmployeeSpec : Specification<ProbationExtensionRequest>
{
    public PendingProbationExtensionForEmployeeSpec(EmployeeId employeeId)
    {
        Query.Where(request => request.EmployeeId == employeeId
                               && request.Status == ProbationExtensionStatus.Pending);
        Query.AsNoTracking();
    }
}

/// <summary>One request by id, tracked for a lifecycle decision.</summary>
internal sealed class ProbationExtensionByIdSpec : SingleResultSpecification<ProbationExtensionRequest>
{
    public ProbationExtensionByIdSpec(ProbationExtensionRequestId id)
    {
        Query.Where(request => request.Id == id);
    }
}

internal sealed class ProbationExtensionListSpec : Specification<ProbationExtensionRequest>
{
    public ProbationExtensionListSpec(
        int page,
        int pageSize,
        IReadOnlyList<Expression<Func<ProbationExtensionRequest, bool>>> filters,
        IReadOnlyCollection<EmployeeId>? employeeIds)
    {
        ProbationExtensionFilters.Apply(Query, filters, employeeIds);
        Query.Include(request => request.Employee);
        Query.OrderByDescending(request => request.RequestedAtUtc);
        Query.AsNoTracking();
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}

internal sealed class ProbationExtensionCountSpec : Specification<ProbationExtensionRequest>
{
    public ProbationExtensionCountSpec(
        IReadOnlyList<Expression<Func<ProbationExtensionRequest, bool>>> filters,
        IReadOnlyCollection<EmployeeId>? employeeIds)
    {
        ProbationExtensionFilters.Apply(Query, filters, employeeIds);
        Query.AsNoTracking();
    }
}

internal static class ProbationExtensionFilters
{
    internal static void Apply(
        ISpecificationBuilder<ProbationExtensionRequest> query,
        IReadOnlyList<Expression<Func<ProbationExtensionRequest, bool>>> filters,
        IReadOnlyCollection<EmployeeId>? employeeIds)
    {
        // Scope first, filters second: a filter row narrows the caller's own scope and can never
        // reach outside it.
        //
        // Null means unrestricted; an empty set means nothing matches, which is the correct
        // answer for a caller with no standing rather than an unfiltered query.
        if (employeeIds is not null)
        {
            query.Where(request => employeeIds.Contains(request.EmployeeId));
        }

        FilterApplier.ApplyTo(query, filters);
    }
}
