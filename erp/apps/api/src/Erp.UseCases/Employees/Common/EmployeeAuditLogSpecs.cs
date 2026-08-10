using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.UseCases.Employees.Common;

internal static class EmployeeAuditLogFilters
{
    /// <summary>
    /// The UI renders timestamps in Jakarta time, so a day filter has to mean a Jakarta day —
    /// filtering on UTC midnight would misfile everything between 00:00 and 07:00 local into
    /// the previous day. Single-timezone org, so this is a constant rather than a lookup.
    /// </summary>
    private static readonly DateTimeZone DisplayZone = DateTimeZoneProviders.Tzdb["Asia/Jakarta"];

    internal static void Apply(
        ISpecificationBuilder<EmployeeAuditLog> query,
        Guid? employeeId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? eventType)
    {
        if (employeeId is { } id)
        {
            query.Where(log => log.EmployeeId == new EmployeeId(id));
        }

        if (dateFrom is { } from)
        {
            var start = StartOfDay(from);
            query.Where(log => log.OccurredAtUtc >= start);
        }

        if (dateTo is { } to)
        {
            // Exclusive upper bound: start of the *next* Jakarta day.
            var end = StartOfDay(to.AddDays(1));
            query.Where(log => log.OccurredAtUtc < end);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query.Where(log => log.EventType == eventType);
        }
    }

    private static Instant StartOfDay(DateOnly date) =>
        LocalDate.FromDateOnly(date).AtStartOfDayInZone(DisplayZone).ToInstant();
}

internal sealed class EmployeeAuditLogListSpec : Specification<EmployeeAuditLog>
{
    public EmployeeAuditLogListSpec(
        int page, int pageSize, Guid? employeeId, DateOnly? dateFrom, DateOnly? dateTo, string? eventType)
    {
        EmployeeAuditLogFilters.Apply(Query, employeeId, dateFrom, dateTo, eventType);
        Query.OrderByDescending(log => log.OccurredAtUtc);
        Query.AsNoTracking();
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}

internal sealed class EmployeeAuditLogCountSpec : Specification<EmployeeAuditLog>
{
    public EmployeeAuditLogCountSpec(Guid? employeeId, DateOnly? dateFrom, DateOnly? dateTo, string? eventType)
    {
        EmployeeAuditLogFilters.Apply(Query, employeeId, dateFrom, dateTo, eventType);
        Query.AsNoTracking();
    }
}

/// <summary>All matching rows, unpaginated — caller must cap via a prior CountAsync check.</summary>
internal sealed class EmployeeAuditLogExportSpec : Specification<EmployeeAuditLog>
{
    public EmployeeAuditLogExportSpec(Guid? employeeId, DateOnly? dateFrom, DateOnly? dateTo, string? eventType)
    {
        EmployeeAuditLogFilters.Apply(Query, employeeId, dateFrom, dateTo, eventType);
        Query.OrderByDescending(log => log.OccurredAtUtc);
        Query.AsNoTracking();
    }
}

internal sealed class EmployeeNamesByIdSpec : Specification<Employee>
{
    public EmployeeNamesByIdSpec(IReadOnlyCollection<EmployeeId> ids)
    {
        Query.Where(employee => ids.Contains(employee.Id));
        Query.AsNoTracking();
    }
}
