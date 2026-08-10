namespace Erp.UseCases.Employees.ListEmployeeAuditLog;

public sealed record ListEmployeeAuditLogQuery(
    int Page,
    int PageSize,
    Guid? EmployeeId,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    string? EventType);
