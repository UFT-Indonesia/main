using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;
using Erp.UseCases.Employees.Common;
using FluentAssertions;
using NodaTime;
using static Erp.UnitTests.UseCases.FilterRows;

namespace Erp.UnitTests.UseCases;

/// <summary>
/// The UI shows Jakarta timestamps, so a day filter has to cover the Jakarta day —
/// UTC midnight would push 00:00–07:00 WIB into the previous day's results.
/// </summary>
public class EmployeeAuditLogFilterTests
{
    private static readonly Caller Owner =
        new(Guid.NewGuid(), EmployeeRole.Owner, new EmployeeId(Guid.NewGuid()), "Owner");

    private static EmployeeAuditLog RowAt(Instant occurredAt) =>
        EmployeeAuditLog.Create(EmployeeId.New(), "employee.salary_changed", occurredAt, null, null);

    // 2026-08-09 01:00 WIB — same instant is 2026-08-08 18:00 UTC.
    private static readonly EmployeeAuditLog EarlyMorningJakarta =
        RowAt(Instant.FromUtc(2026, 8, 8, 18, 0));

    private static EmployeeAuditLogExportSpec SpecFor(string op, string valueJson)
    {
        FilterApplier.TryCompile(
                EmployeeAuditLogFilterFields.Fields,
                Rows(Row("occurredAt", op, valueJson)),
                Owner,
                out var filters,
                out var failure)
            .Should().BeTrue(failure.Message);

        return new EmployeeAuditLogExportSpec(filters);
    }

    [Fact]
    public void After_includes_rows_in_the_early_jakarta_morning()
    {
        SpecFor(FilterOps.Is, "\"2026-08-09\"")
            .Evaluate([EarlyMorningJakarta]).Should().ContainSingle();
    }

    [Fact]
    public void A_day_filter_excludes_rows_that_fall_on_the_next_jakarta_day()
    {
        SpecFor(FilterOps.Is, "\"2026-08-08\"")
            .Evaluate([EarlyMorningJakarta]).Should().BeEmpty();
    }

    [Fact]
    public void A_day_filter_includes_the_whole_of_its_own_jakarta_day()
    {
        // 2026-08-08 23:30 WIB == 2026-08-08 16:30 UTC.
        var lateEvening = RowAt(Instant.FromUtc(2026, 8, 8, 16, 30));

        SpecFor(FilterOps.Is, "\"2026-08-08\"")
            .Evaluate([lateEvening]).Should().ContainSingle();
    }

    [Fact]
    public void Between_covers_both_end_days_in_full()
    {
        // 23:30 WIB on the last day of the range is still inside it.
        var lastDayLate = RowAt(Instant.FromUtc(2026, 8, 10, 16, 30));

        SpecFor(FilterOps.Between, "[\"2026-08-08\",\"2026-08-10\"]")
            .Evaluate([EarlyMorningJakarta, lastDayLate]).Should().HaveCount(2);
    }

    [Fact]
    public void Before_excludes_its_own_day()
    {
        SpecFor(FilterOps.Before, "\"2026-08-09\"")
            .Evaluate([EarlyMorningJakarta]).Should().BeEmpty();
    }
}
