namespace Erp.UseCases.Employees.DeleteEmployee;

public sealed record DeleteEmployeeCommand(Guid EmployeeId, DateOnly? TerminationDate);
