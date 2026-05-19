using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using FluentAssertions;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class EmployeeHierarchyServiceTests
{
    [Fact]
    public async Task ResolveAncestors_returns_empty_for_null_parent()
    {
        var lookup = Substitute.For<IEmployeeHierarchyLookup>();

        var result = await EmployeeHierarchyService.ResolveAncestorsForParentAsync(
            null, lookup, CancellationToken.None);

        result.Should().BeEmpty();
        await lookup.DidNotReceive().AcquireHierarchyLockAsync(Arg.Any<CancellationToken>());
        await lookup.DidNotReceive().GetAncestorsAsync(Arg.Any<EmployeeId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAncestors_acquires_lock_before_reading_chain()
    {
        var lookup = Substitute.For<IEmployeeHierarchyLookup>();
        var calls = new List<string>();
        lookup.AcquireHierarchyLockAsync(Arg.Any<CancellationToken>())
            .Returns(_ => { calls.Add("lock"); return Task.CompletedTask; });
        lookup.ExistsAsync(Arg.Any<EmployeeId>(), Arg.Any<CancellationToken>())
            .Returns(_ => { calls.Add("exists"); return Task.FromResult(true); });
        lookup.GetAncestorsAsync(Arg.Any<EmployeeId>(), Arg.Any<CancellationToken>())
            .Returns(_ => { calls.Add("read"); return Task.FromResult<IReadOnlyList<EmployeeId>>(Array.Empty<EmployeeId>()); });

        var parentId = EmployeeId.New();
        await EmployeeHierarchyService.ResolveAncestorsForParentAsync(parentId, lookup, CancellationToken.None);

        calls.Should().Equal("lock", "exists", "read");
    }

    [Fact]
    public async Task ResolveAncestors_returns_parent_ancestors_only()
    {
        var lookup = Substitute.For<IEmployeeHierarchyLookup>();
        var parentId = EmployeeId.New();
        var grandparentId = EmployeeId.New();
        lookup.ExistsAsync(parentId, Arg.Any<CancellationToken>()).Returns(true);
        lookup.GetAncestorsAsync(parentId, Arg.Any<CancellationToken>())
            .Returns(new[] { grandparentId });

        var chain = await EmployeeHierarchyService.ResolveAncestorsForParentAsync(
            parentId, lookup, CancellationToken.None);

        chain.Should().Equal(grandparentId);
    }

    [Fact]
    public async Task ResolveAncestors_throws_when_chain_hits_safety_cap()
    {
        var lookup = Substitute.For<IEmployeeHierarchyLookup>();
        var parentId = EmployeeId.New();
        lookup.ExistsAsync(parentId, Arg.Any<CancellationToken>()).Returns(true);
        var capped = Enumerable.Range(0, 8).Select(_ => EmployeeId.New()).ToArray();
        lookup.GetAncestorsAsync(parentId, Arg.Any<CancellationToken>())
            .Returns(capped);

        var act = () => EmployeeHierarchyService.ResolveAncestorsForParentAsync(
            parentId, lookup, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Code.Should().Be("employee.hierarchy_corrupted");
    }

    [Fact]
    public async Task ResolveAncestors_throws_when_parent_does_not_exist()
    {
        var lookup = Substitute.For<IEmployeeHierarchyLookup>();
        var parentId = EmployeeId.New();
        lookup.ExistsAsync(parentId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => EmployeeHierarchyService.ResolveAncestorsForParentAsync(
            parentId, lookup, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Code.Should().Be("employee.parent_not_found");
    }
}
