using Erp.Core.Aggregates.Employees;
using Erp.UseCases.Common;

namespace Erp.UseCases.Employees.Common;

/// <summary>
/// Who may read an employee's personal details — national ID, tax ID, salary dates,
/// termination date. The directory itself is open to every employee so pickers and the leave
/// calendar can name people, but a name is all a colleague gets.
/// </summary>
public static class EmployeeVisibility
{
    /// <summary>
    /// Owner reads everyone. A Manager reads their own record and their own direct Staff.
    /// Staff read their own record only — everything else is a name, a role and a status.
    /// </summary>
    public static bool CanReadDetails(Caller caller, EmployeeResult subject)
    {
        if (caller.Role == EmployeeRole.Owner)
        {
            return true;
        }

        if (caller.EmployeeId is not { } callerEmployeeId)
        {
            return false;
        }

        if (subject.Id == callerEmployeeId.Value)
        {
            return true;
        }

        return caller.Role == EmployeeRole.Manager
            && subject.ParentId == callerEmployeeId.Value
            && string.Equals(subject.Role, nameof(EmployeeRole.Staff), StringComparison.Ordinal);
    }

    /// <summary>Pay stays Owner-only, unchanged by the directory opening up.</summary>
    public static bool CanReadWage(Caller caller) => caller.Role == EmployeeRole.Owner;
}
