using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;

namespace Erp.UseCases.Attendance.Common;

/// <summary>
/// Attendance visibility is deliberately wider than attendance authority: a Manager can see
/// the whole company's times so they can cover for each other, but may only create or alter
/// records inside their own reporting line — a manual punch asserts someone was present.
/// </summary>
public static class AttendanceRules
{
    public static bool CanReadAll(Caller caller) =>
        caller.Role is EmployeeRole.Owner or EmployeeRole.Manager;

    /// <summary>Staff are limited to their own record; everyone above them reads all of them.</summary>
    public static bool CanRead(Caller caller, EmployeeId subjectId) =>
        CanReadAll(caller) || caller.EmployeeId == subjectId;

    /// <summary>Owner writes anywhere, a Manager only for themselves and their direct Staff, Staff never.</summary>
    public static bool CanWriteFor(Caller caller, Employee subject) => caller.Role switch
    {
        EmployeeRole.Owner => true,
        EmployeeRole.Manager => OrgScope.IsSelf(caller, subject) || OrgScope.IsDirectStaffOf(caller, subject),
        _ => false,
    };
}
