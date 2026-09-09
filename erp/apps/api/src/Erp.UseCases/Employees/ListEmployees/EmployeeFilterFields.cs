using Erp.Core.Aggregates.Employees;
using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;
using Erp.UseCases.Employees.Common;

namespace Erp.UseCases.Employees.ListEmployees;

/// <summary>
/// What the Karyawan filter builder may ask about. Mirrored by
/// <c>apps/web/src/lib/filters/employee-fields.ts</c>; the two drift only until the next
/// request, since an unknown key here is a 400 there.
/// </summary>
public static class EmployeeFilterFields
{
    public static readonly FilterFieldMap<Employee> Fields = new FilterFieldMap<Employee>()
        .Text("fullName", employee => employee.FullName)
        .Enum("role", employee => employee.Role)
        .Enum("status", employee => employee.Status)
        .Date("hireDate", employee => employee.HireDate)

        // NIK and NPWP are stored through a whole-property value converter, so EF sees one
        // opaque column and cannot translate Contains or StartsWith over it. Equality only,
        // parsed through the domain factory so a malformed NIK is a 400 rather than a query
        // that quietly matches nothing.
        .ConvertedText(
            "nik",
            employee => employee.Nik,
            Nik.Create,
            sample: "3201234567890123",
            visible: EmployeeVisibility.CanFilterRedacted)
        .ConvertedText(
            "npwp",
            employee => employee.Npwp,
            Npwp.Create,
            sample: "091234567890123",
            nullable: true,
            visible: EmployeeVisibility.CanFilterRedacted)

        .Number(
            "monthlyWage",
            employee => employee.MonthlyWage.Amount,
            visible: EmployeeVisibility.CanFilterRedacted)
        .Date(
            "effectiveSalaryFrom",
            employee => employee.EffectiveSalaryFrom,
            visible: EmployeeVisibility.CanFilterRedacted)
        .Date(
            "terminationDate",
            employee => employee.TerminationDate,
            visible: EmployeeVisibility.CanFilterRedacted);
}
