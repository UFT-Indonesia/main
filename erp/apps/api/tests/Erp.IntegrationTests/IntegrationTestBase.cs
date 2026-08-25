using System.Net.Http.Headers;
using System.Net.Http.Json;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Identity;
using Erp.SharedKernel.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Erp.IntegrationTests;

[Collection(ErpApiCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private const string TestPassword = "Passw0rd!";
    private static int _nikCounter;

    protected ErpApiFactory Factory { get; }

    protected IntegrationTestBase(ErpApiFactory factory)
    {
        Factory = factory;
    }

    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>NIKs are unique-indexed, so every fixture employee needs its own.</summary>
    private static string NextNik() => (30_000_000_000_000_00L + Interlocked.Increment(ref _nikCounter))
        .ToString()
        .PadLeft(Nik.Length, '0')[..Nik.Length];

    /// <summary>
    /// A null <paramref name="hireDate"/> leaves the employee permanently off probation, which is
    /// what every fixture that does not care about probation wants.
    /// </summary>
    protected async Task<Employee> CreateEmployeeAsync(
        EmployeeRole role,
        string fullName,
        EmployeeId? parentId = null,
        LocalDate? hireDate = null)
    {
        await using var db = Factory.CreateDbContext();
        var employee = Employee.Create(
            fullName,
            Nik.Create(NextNik()),
            Money.Idr(5_000_000m),
            new LocalDate(2026, 1, 1),
            role,
            role == EmployeeRole.Owner ? null : parentId ?? (await EnsureOwnerAsync(db)).Id,
            hireDate: hireDate);

        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee;
    }

    private async Task<Employee> EnsureOwnerAsync(Erp.Infrastructure.Persistence.AppDbContext db)
    {
        var owner = Employee.Create(
            "Fixture Owner",
            Nik.Create(NextNik()),
            Money.Idr(9_000_000m),
            new LocalDate(2026, 1, 1),
            EmployeeRole.Owner);

        db.Employees.Add(owner);
        await db.SaveChangesAsync();
        return owner;
    }

    /// <summary>Creates a usable login for the employee and returns a bearer-authenticated client.</summary>
    protected async Task<HttpClient> CreateClientForAsync(Employee employee)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = $"user-{employee.Id.Value:N}",
                Email = $"{employee.Id.Value:N}@erp.test",
                EmailConfirmed = true,
                FullName = employee.FullName,
                EmployeeId = employee.Id.Value,
                // Otherwise MustChangePasswordMiddleware blocks everything outside /api/auth.
                MustChangePassword = false,
            };

            var created = await userManager.CreateAsync(user, TestPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to create test account: "
                        + string.Join("; ", created.Errors.Select(error => error.Description)));
            }
        }

        return await LoginAsync($"user-{employee.Id.Value:N}", TestPassword);
    }

    /// <summary>
    /// A login with no employee record behind it — the legacy shape Caller documents, where
    /// AccountIdentityResolver falls back to the stored Identity roles.
    /// </summary>
    protected async Task<HttpClient> CreateClientForAccountWithoutEmployeeAsync(EmployeeRole role)
    {
        var username = $"detached-{Guid.NewGuid():N}";
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = username,
                Email = $"{username}@erp.test",
                EmailConfirmed = true,
                FullName = "Detached Account",
                EmployeeId = null,
                MustChangePassword = false,
            };

            var created = await userManager.CreateAsync(user, TestPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to create detached test account: "
                        + string.Join("; ", created.Errors.Select(error => error.Description)));
            }

            await userManager.AddToRoleAsync(user, role.ToString());
        }

        return await LoginAsync(username, TestPassword);
    }

    /// <summary>Logs in through the real endpoint, so tokens carry whatever claims the app stamps.</summary>
    protected async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginBody>()
            ?? throw new InvalidOperationException("Login returned no body.");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.AccessToken);
        return client;
    }

    protected sealed record LoginBody(string AccessToken, LoginUser User);

    protected sealed record LoginUser(Guid Id, Guid? EmployeeId, string[] Roles);
}
