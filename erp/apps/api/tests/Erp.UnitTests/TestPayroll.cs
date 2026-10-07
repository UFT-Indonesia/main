using Ardalis.Specification;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Interfaces;
using NSubstitute;

namespace Erp.UnitTests;

internal static class TestPayroll
{
    /// <summary>No payroll month has been closed — the world every pre-GSS03 leave test lives in.</summary>
    internal static IReadRepository<LeaveDeductionMonth> NoMonths()
    {
        var repository = Substitute.For<IReadRepository<LeaveDeductionMonth>>();
        repository.ListAsync(Arg.Any<ISpecification<LeaveDeductionMonth>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveDeductionMonth>());
        return repository;
    }

    /// <summary>A repository holding exactly the given closed months.</summary>
    internal static IReadRepository<LeaveDeductionMonth> Closed(params NodaTime.LocalDate[] months)
    {
        var repository = Substitute.For<IReadRepository<LeaveDeductionMonth>>();
        repository.ListAsync(Arg.Any<ISpecification<LeaveDeductionMonth>>(), Arg.Any<CancellationToken>())
            .Returns(months.Select(m => new LeaveDeductionMonth(m, Guid.NewGuid(), "Owner", NodaTime.Instant.FromUtc(2026, 1, 1, 0, 0))).ToList());
        return repository;
    }

    internal static IReadRepository<LeaveDeductionLine> NoLines()
    {
        var repository = Substitute.For<IReadRepository<LeaveDeductionLine>>();
        repository.ListAsync(Arg.Any<ISpecification<LeaveDeductionLine>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveDeductionLine>());
        return repository;
    }
}
