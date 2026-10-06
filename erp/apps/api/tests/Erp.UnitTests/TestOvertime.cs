using Ardalis.Specification;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using NSubstitute;

namespace Erp.UnitTests;

internal static class TestOvertime
{
    /// <summary>A repository with no overtime assignments in it — the world every pre-overtime test lives in.</summary>
    internal static IReadRepository<OvertimeAssignment> None()
    {
        var repository = Substitute.For<IReadRepository<OvertimeAssignment>>();
        repository.ListAsync(Arg.Any<ISpecification<OvertimeAssignment>>(), Arg.Any<CancellationToken>())
            .Returns(new List<OvertimeAssignment>());
        return repository;
    }
}
