using Ardalis.Specification;
using Erp.Core.Aggregates.Probation;
using Erp.SharedKernel.Identity;

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
        int page, int pageSize, ProbationExtensionStatus? status, IReadOnlyCollection<EmployeeId>? employeeIds)
    {
        ProbationExtensionFilters.Apply(Query, status, employeeIds);
        Query.Include(request => request.Employee);
        Query.OrderByDescending(request => request.RequestedAtUtc);
        Query.AsNoTracking();
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}

internal sealed class ProbationExtensionCountSpec : Specification<ProbationExtensionRequest>
{
    public ProbationExtensionCountSpec(ProbationExtensionStatus? status, IReadOnlyCollection<EmployeeId>? employeeIds)
    {
        ProbationExtensionFilters.Apply(Query, status, employeeIds);
        Query.AsNoTracking();
    }
}

internal static class ProbationExtensionFilters
{
    internal static void Apply(
        ISpecificationBuilder<ProbationExtensionRequest> query,
        ProbationExtensionStatus? status,
        IReadOnlyCollection<EmployeeId>? employeeIds)
    {
        if (status.HasValue)
        {
            query.Where(request => request.Status == status.Value);
        }

        // Null means unrestricted; an empty set means nothing matches, which is the correct
        // answer for a caller with no standing rather than an unfiltered query.
        if (employeeIds is not null)
        {
            query.Where(request => employeeIds.Contains(request.EmployeeId));
        }
    }
}
