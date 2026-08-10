using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.UseCases;

/// <summary>
/// The UI shows Jakarta timestamps, so a day filter has to cover the Jakarta day —
/// UTC midnight would push 00:00–07:00 WIB into the previous day's results.
/// </summary>
public class EmployeeAuditLogFilterTests
{
    private static EmployeeAuditLog RowAt(Instant occurredAt) =>
        EmployeeAuditLog.Create(EmployeeId.New(), "employee.salary_changed", occurredAt, null, null);

    // 2026-08-09 01:00 WIB — same instant is 2026-08-08 18:00 UTC.
    private static readonly EmployeeAuditLog EarlyMorningJakarta =
        RowAt(Instant.FromUtc(2026, 8, 8, 18, 0));

    [Fact]
    public void DateFrom_includes_rows_in_the_early_jakarta_morning()
    {
        var spec = new EmployeeAuditLogExportSpec(
            employeeId: null, dateFrom: new DateOnly(2026, 8, 9), dateTo: null, eventType: null);

        spec.Evaluate([EarlyMorningJakarta]).Should().ContainSingle();
    }

    [Fact]
    public void DateTo_excludes_rows_that_fall_on_the_next_jakarta_day()
    {
        var spec = new EmployeeAuditLogExportSpec(
            employeeId: null, dateFrom: null, dateTo: new DateOnly(2026, 8, 8), eventType: null);

        spec.Evaluate([EarlyMorningJakarta]).Should().BeEmpty();
    }

    [Fact]
    public void DateTo_includes_the_whole_of_its_own_jakarta_day()
    {
        // 2026-08-08 23:30 WIB == 2026-08-08 16:30 UTC.
        var lateEvening = RowAt(Instant.FromUtc(2026, 8, 8, 16, 30));

        var spec = new EmployeeAuditLogExportSpec(
            employeeId: null, dateFrom: null, dateTo: new DateOnly(2026, 8, 8), eventType: null);

        spec.Evaluate([lateEvening]).Should().ContainSingle();
    }
}
