using Erp.UseCases.Common;

namespace Erp.UseCases.Employees.SetProbationEnd;

/// <summary>Null <paramref name="EndsOn"/> clears the override, restoring the three-month default.</summary>
public sealed record SetProbationEndCommand(Guid EmployeeId, DateOnly? EndsOn, Caller Caller);
