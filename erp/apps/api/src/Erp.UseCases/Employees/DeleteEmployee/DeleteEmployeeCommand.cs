using Erp.UseCases.Common;

namespace Erp.UseCases.Employees.DeleteEmployee;

public sealed record DeleteEmployeeCommand(Guid EmployeeId, DateOnly? TerminationDate, Caller Caller);
