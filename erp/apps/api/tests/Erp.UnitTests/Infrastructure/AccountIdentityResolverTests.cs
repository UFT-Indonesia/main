using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Identity;
using Erp.Infrastructure.Persistence;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Diagnostics.Internal;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.Infrastructure;

public class AccountIdentityResolverTests
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AccountIdentityResolver _resolver;

    public AccountIdentityResolverTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);

        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            null, null, null, null, null, null, null, null);

        _resolver = new AccountIdentityResolver(_db, _userManager);
    }

    private async Task<Employee> SeedEmployeeAsync(EmployeeRole role)
    {
        var employee = Employee.Create(
            "Test Employee",
            Nik.Create("3201234567890123"),
            Money.Idr(5_000_000m),
            new LocalDate(2026, 1, 1),
            role,
            role == EmployeeRole.Owner ? null : EmployeeId.New());

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        return employee;
    }

    [Theory]
    [InlineData(EmployeeRole.Owner)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Staff)]
    public async Task Resolves_role_from_the_linked_employee(EmployeeRole role)
    {
        var employee = await SeedEmployeeAsync(role);
        var user = new ApplicationUser { EmployeeId = employee.Id.Value };

        var identity = await _resolver.ResolveAsync(user, CancellationToken.None);

        identity.Role.Should().Be(role);
        identity.EmployeeId.Should().Be(employee.Id.Value);
    }

    [Fact]
    public async Task Employee_role_wins_over_a_stale_stored_identity_role()
    {
        var employee = await SeedEmployeeAsync(EmployeeRole.Staff);
        var user = new ApplicationUser { EmployeeId = employee.Id.Value };
        // The account was left holding Owner by an older dual-write; it must not count.
        _userManager.GetRolesAsync(user).Returns([nameof(EmployeeRole.Owner)]);

        var identity = await _resolver.ResolveAsync(user, CancellationToken.None);

        identity.Role.Should().Be(EmployeeRole.Staff);
    }

    [Fact]
    public async Task Falls_back_to_stored_roles_when_the_account_has_no_employee()
    {
        var user = new ApplicationUser { EmployeeId = null };
        _userManager.GetRolesAsync(user).Returns([nameof(EmployeeRole.Manager)]);

        var identity = await _resolver.ResolveAsync(user, CancellationToken.None);

        identity.Role.Should().Be(EmployeeRole.Manager);
        identity.EmployeeId.Should().BeNull();
    }

    [Fact]
    public async Task Falls_back_when_the_linked_employee_row_is_missing()
    {
        var user = new ApplicationUser { EmployeeId = Guid.NewGuid() };
        _userManager.GetRolesAsync(user).Returns([nameof(EmployeeRole.Manager)]);

        var identity = await _resolver.ResolveAsync(user, CancellationToken.None);

        identity.Role.Should().Be(EmployeeRole.Manager);
    }

    [Fact]
    public async Task Defaults_to_the_least_privilege_when_nothing_identifies_the_account()
    {
        var user = new ApplicationUser { EmployeeId = null };
        _userManager.GetRolesAsync(user).Returns([]);

        var identity = await _resolver.ResolveAsync(user, CancellationToken.None);

        identity.Role.Should().Be(EmployeeRole.Staff);
    }

    [Fact]
    public void Most_privileged_stored_role_wins_when_several_are_present()
    {
        EmployeeRoleNames.MostPrivileged([nameof(EmployeeRole.Staff), nameof(EmployeeRole.Owner)])
            .Should().Be(EmployeeRole.Owner);
        EmployeeRoleNames.MostPrivileged([nameof(EmployeeRole.Staff), nameof(EmployeeRole.Manager)])
            .Should().Be(EmployeeRole.Manager);
        EmployeeRoleNames.MostPrivileged(["not-a-role"]).Should().Be(EmployeeRole.Staff);
    }
}
