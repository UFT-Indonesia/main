using System.Net;
using System.Net.Http.Json;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Probation;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Erp.IntegrationTests;

/// <summary>
/// Leave and probation decisions write to more than one aggregate. Each test fails the LATER
/// write, so the only thing that can undo the earlier one is the handler's transaction.
/// </summary>
public class LeaveProbationTransactionTests : IntegrationTestBase
{
    public LeaveProbationTransactionTests(ErpApiFactory factory) : base(factory) { }

    private static LocalDate TodayInJakarta =>
        SystemClock.Instance.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb["Asia/Jakarta"]).Date;

    private sealed record Created(Guid Id);

    /// <summary>
    /// Throws on the save that flips the OnLeave badge — the step after the leave day is
    /// materialized. Every other employee save goes through, so the approval itself succeeds.
    /// </summary>
    private sealed class ThrowingOnLeaveEmployeeRepository(AppDbContext db) : EfRepository<Employee>(db)
    {
        public override Task<int> UpdateAsync(Employee entity, CancellationToken cancellationToken = default)
            => entity.Status == EmployeeStatus.OnLeave
                ? throw new InvalidOperationException("Simulated status reconcile failure.")
                : base.UpdateAsync(entity, cancellationToken);
    }

    /// <summary>The employee save throws, after the request's own decision has been saved.</summary>
    private sealed class ThrowingEmployeeRepository(AppDbContext db) : EfRepository<Employee>(db)
    {
        public override Task<int> UpdateAsync(Employee entity, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated probation end save failure.");
    }

    [Fact]
    public async Task A_failed_status_reconcile_rolls_back_the_materialized_leave_day()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Biasa", owner.Id);

        // Covers today, so the reconcile has a badge to flip.
        var staffClient = await CreateClientForAsync(staff);
        var filed = await staffClient.PostAsJsonAsync("/api/leave/", new
        {
            employeeId = staff.Id.Value,
            type = "Annual",
            startDate = TodayInJakarta.ToString("yyyy-MM-dd", null),
            endDate = TodayInJakarta.PlusDays(3).ToString("yyyy-MM-dd", null),
            reason = "cuti",
        });
        filed.StatusCode.Should().Be(HttpStatusCode.Created);
        var request = (await filed.Content.ReadFromJsonAsync<Created>())!;

        var (host, client) = await CreateClientWithFailingHostAsync(
            owner, services => services.AddScoped<IRepository<Employee>, ThrowingOnLeaveEmployeeRepository>());
        await using var _ = host;
        var approve = await AfterBackgroundWorkAsync(
            host.Services, () => client.PostAsJsonAsync($"/api/leave/{request.Id}/approve", new { }));

        // The approval and the day sync are separate steps: the approval stands, and the sync
        // is all-or-nothing on its own.
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = Factory.CreateDbContext();
        var leaveId = new LeaveRequestId(request.Id);
        (await db.LeaveRequests.SingleAsync(r => r.Id == leaveId)).Status.Should().Be(LeaveRequestStatus.Approved);
        (await db.AttendanceDays.AnyAsync(day => day.EmployeeId == staff.Id)).Should().BeFalse(
            "the leave day must not survive a sync that failed half-way");
        (await db.Employees.SingleAsync(e => e.Id == staff.Id)).Status.Should().NotBe(EmployeeStatus.OnLeave);
    }

    [Fact]
    public async Task A_failed_probation_end_save_rolls_back_the_decision()
    {
        var owner = await CreateEmployeeAsync(EmployeeRole.Owner, "Owner Utama");
        var manager = await CreateEmployeeAsync(EmployeeRole.Manager, "Manager Satu", owner.Id);
        var hired = TodayInJakarta;
        var staff = await CreateEmployeeAsync(EmployeeRole.Staff, "Staff Baru", manager.Id, hired);

        var managerClient = await CreateClientForAsync(manager);
        var filed = await managerClient.PostAsJsonAsync("/api/probation/", new
        {
            employeeId = staff.Id.Value,
            proposedEndsOn = hired.PlusMonths(6).ToString("yyyy-MM-dd", null),
            reason = "Perlu waktu tambahan untuk penilaian.",
        });
        filed.StatusCode.Should().Be(HttpStatusCode.Created);
        var request = (await filed.Content.ReadFromJsonAsync<Created>())!;

        var (host, client) = await CreateClientWithFailingHostAsync(
            owner, services => services.AddScoped<IRepository<Employee>, ThrowingEmployeeRepository>());
        await using var _ = host;
        var approve = await client.PostAsJsonAsync(
            $"/api/probation/{request.Id}/approve", new { note = (string?)null });

        approve.IsSuccessStatusCode.Should().BeFalse();
        await using var db = Factory.CreateDbContext();
        var requestId = new ProbationExtensionRequestId(request.Id);
        (await db.ProbationExtensionRequests.SingleAsync(r => r.Id == requestId))
            .Status.Should().Be(ProbationExtensionStatus.Pending,
                "a request cannot read Approved while the probation end it approved never moved");
    }
}
