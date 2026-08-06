using Erp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace Erp.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway Postgres container. Requires a running Docker
/// daemon — without one, every test in this project fails at container start.
/// </summary>
public sealed class ErpApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Base64 of 32 zero bytes — long enough to satisfy the signing-key validator.</summary>
    private static readonly string TestSigningKey = Convert.ToBase64String(new byte[32]);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private Respawner _respawner = default!;
    private NpgsqlConnection _resetConnection = default!;

    public const string SeededOwnerEmail = "owner@erp.test";
    public const string SeededOwnerPassword = "Owner12345";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Program.cs seeds Identity at startup but never migrates, so the schema has to
        // exist before the host boots.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        // Force the host (and its Identity seeding) to build now, so the first test does
        // not pay for it and the seeded owner exists before any snapshot is taken.
        using var _ = CreateClient();

        _resetConnection = new NpgsqlConnection(_postgres.GetConnectionString());
        await _resetConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_resetConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore =
            [
                // Schema bookkeeping.
                "__EFMigrationsHistory",
                // Seeded by migration and resolved at DI scope creation — deleting it makes
                // every subsequent request fail before it reaches an endpoint.
                "AttendancePolicies",
                // Wolverine's and Hangfire's own bookkeeping; they own these lifecycles.
                "wolverine_incoming_envelopes",
                "wolverine_outgoing_envelopes",
                "wolverine_dead_letters",
            ],
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Appended last so it outranks appsettings and any .env the app picks up.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
                ["Jwt:Issuer"] = "erp.test",
                ["Jwt:Audience"] = "erp.test",
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "14",
                ["DeviceIngest:HmacSecret"] = "integration_test_device_secret",
                ["DeviceIngest:ToleranceSeconds"] = "300",
                ["Hangfire:DashboardEnabled"] = "false",
                ["Seed:Owner:Email"] = SeededOwnerEmail,
                ["Seed:Owner:Password"] = SeededOwnerPassword,
                ["Seed:Owner:FullName"] = "Seeded Owner",
                ["Seed:Owner:Nik"] = "9999999999999999",
            }));
    }

    /// <summary>Empties every table the tests write to, leaving migration-seeded rows intact.</summary>
    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_resetConnection);

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_resetConnection is not null)
        {
            await _resetConnection.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ErpApiCollection : ICollectionFixture<ErpApiFactory>
{
    public const string Name = "erp-api";
}
