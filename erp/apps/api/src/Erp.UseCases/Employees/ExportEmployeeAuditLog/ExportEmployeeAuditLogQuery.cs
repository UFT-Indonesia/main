namespace Erp.UseCases.Employees.ExportEmployeeAuditLog;

public sealed record ExportEmployeeAuditLogQuery(
    Guid? EmployeeId,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    string? EventType);
