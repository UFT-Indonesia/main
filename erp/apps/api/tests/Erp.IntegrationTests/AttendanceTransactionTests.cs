using System.Net;
using System.Net.Http.Json;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Erp.IntegrationTests;

/// <summary>
/// The manual-punch, punch-edit and policy-update handlers each make several writes in one
/// [Transactional] unit. The attribute fails silently if Wolverine cannot find the DbContext
/// behind the repositories, so only a forced write failure proves it actually applies.
/// </summary>
public class AttendanceTransactionTests : IntegrationTestBase
{
    public AttendanceTransactionTests(ErpApiFactory factory) : base(factory) { }

    // 08:00 in Asia/Jakarta on a past Wednesday.
    private static readonly DateTimeOffset PunchAt = new(2026, 8, 5, 1, 0, 0, TimeSpan.Zero);

    /// <summary>Every write to the materialized day throws, standing in for any recompute failure.</summary>
    private sealed class ThrowingAttendanceDayRepository(AppDbContext db) : EfRepository<AttendanceDay>(db)
    {
        public override Task<AttendanceDay> AddAsync(AttendanceDay entity, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated recompute failure.");

        public override Task<int> UpdateAsync(AttendanceDay entity, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated recompute failure.");
    }

    /// <summary>The policy's own save throws, after its history row has already been added.</summary>
    private sealed class ThrowingAttendancePolicyRepository(AppDbContext db) : EfRepository<AttendancePolicy>(db)
    {
        public override Task<int> UpdateAsync(AttendancePolicy entity, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated policy save failure.");
    }

    /// <summary>
    /// A second host over the same database with one repository swapped for a throwing one. The token comes from
    /// the shared host — both sign with the same test key, so it is valid on either. The caller
    /// disposes the host, so its throwing repository never picks up another test's queued work.
    /// </summary>
    private async Task<(WebApplicationFactory<Program> Host, HttpClient Client)> CreateClientWithFailingHostAsync(
        Employee caller,
        Action<IServiceCollection> swapRepository)
    {
        var authorized = await CreateClientForAsync(caller);
        var host = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(swapRepository));

        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = authorized.DefaultRequestHeaders.Authorization;
        return (host, client);
    }

    [Fact]
    public async Task A_manual_punch_updates_the_day_before_the_response_returns()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);

        var client = await CreateClientForAsync(owner);
        var response = await client.PostAsJsonAsync("/api/attendance/manual-logs", new
        {
            employeeId = staff.Id.Value,
            punchedAtUtc = PunchAt,
            punchType = "In",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await using var db = Factory.CreateDbContext();
        (await db.AttendanceDays.AnyAsync(day => day.EmployeeId == staff.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task A_failed_recompute_rolls_back_the_manual_punch()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);

        var (host, client) = await CreateClientWithFailingHostAsync(
            owner, services => services.AddScoped<IRepository<AttendanceDay>, ThrowingAttendanceDayRepository>());
        await using var _ = host;
        var response = await client.PostAsJsonAsync("/api/attendance/manual-logs", new
        {
            employeeId = staff.Id.Value,
            punchedAtUtc = PunchAt,
            punchType = "In",
        });

        response.IsSuccessStatusCode.Should().BeFalse();
        await using var db = Factory.CreateDbContext();
        (await db.AttendanceLogs.AnyAsync(log => log.EmployeeId == staff.Id)).Should().BeFalse(
            "the punch must not survive a recompute that failed, or a retry would duplicate it");
    }

    [Fact]
    public async Task A_failed_recompute_rolls_back_the_punch_edit()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);

        var original = Instant.FromDateTimeOffset(PunchAt);
        var log = AttendanceLog.Manual(staff.Id, original, PunchType.In, Guid.NewGuid());
        await using (var seed = Factory.CreateDbContext())
        {
            seed.AttendanceLogs.Add(log);
            await seed.SaveChangesAsync();
        }

        var (host, client) = await CreateClientWithFailingHostAsync(
            owner, services => services.AddScoped<IRepository<AttendanceDay>, ThrowingAttendanceDayRepository>());
        await using var _ = host;
        var response = await client.PatchAsJsonAsync($"/api/attendance/logs/{log.Id.Value}", new
        {
            id = log.Id.Value,
            punchedAtUtc = PunchAt.AddHours(1),
            punchType = "In",
        });

        response.IsSuccessStatusCode.Should().BeFalse();
        await using var db = Factory.CreateDbContext();
        var stored = await db.AttendanceLogs.SingleAsync(l => l.Id == log.Id);
        stored.PunchedAtUtc.Should().Be(original);
    }

    [Fact]
    public async Task A_failed_policy_save_rolls_back_its_history_row()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");

        var (host, client) = await CreateClientWithFailingHostAsync(
            owner, services => services.AddScoped<IRepository<AttendancePolicy>, ThrowingAttendancePolicyRepository>());
        await using var _ = host;
        var response = await client.PutAsJsonAsync("/api/attendance/policy", new
        {
            shiftStart = "08:00",
            shiftEnd = "17:00",
            clockInGraceMinutes = 10,
            clockOutGraceMinutes = 10,
            timeZoneId = "Asia/Jakarta",
            maxIzinHours = 4,
        });

        response.IsSuccessStatusCode.Should().BeFalse();
        await using var db = Factory.CreateDbContext();
        (await db.AttendancePolicyHistories.AnyAsync()).Should().BeFalse(
            "the audit trail must not record a policy change that never took effect");
    }
}
