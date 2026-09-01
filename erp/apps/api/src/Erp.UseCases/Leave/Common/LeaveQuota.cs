using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using NodaTime;

namespace Erp.UseCases.Leave.Common;

/// <summary>
/// How many days of each leave type an employee gets in a given year, and how many they have
/// already used. Pure functions over an <see cref="Employee"/> and their approved requests —
/// nothing about a balance is stored, so cancelling leave frees the days by itself and a
/// balance can never drift out of step with the requests it is derived from.
/// </summary>
public static class LeaveQuota
{
    /// <summary>Full-year annual entitlement for a confirmed employee.</summary>
    public const int FullAnnualDays = 12;

    /// <summary>
    /// Full-year entitlement for the types that are not prorated by probation. Unlike Annual
    /// these are flat: someone still on probation with flu gets the same thirty sick days as
    /// anyone else, because the alternative is an absence that cannot be recorded at all.
    /// </summary>
    public const int FullSickDays = 30;
    public const int FullPermissionDays = 6;
    public const int FullUnpaidDays = 30;

    /// <summary>
    /// The company-wide default for a type before any per-employee override. Annual is absent
    /// here on purpose — it is the only type whose default depends on the employee, so it is
    /// computed by <see cref="AnnualEntitlement"/> instead.
    /// </summary>
    public static int DefaultDays(LeaveType type) => type switch
    {
        LeaveType.Sick => FullSickDays,
        LeaveType.Permission => FullPermissionDays,
        LeaveType.Unpaid => FullUnpaidDays,
        LeaveType.Annual => FullAnnualDays,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown leave type."),
    };

    /// <summary>
    /// Annual days for the year, prorated by when probation ends. One day per remaining whole
    /// month, so the month probation ends in does not count.
    /// <code>
    /// null              → 12   // never on probation
    /// ends before year  → 12   // confirmed in an earlier year
    /// ends after year   → 0    // on probation for the whole year
    /// otherwise         → 12 - month
    /// </code>
    /// Known wart: confirmation on the 1st of a month loses that month. Capped at one day, once.
    /// </summary>
    public static int AnnualEntitlement(LocalDate? probationEndsOn, int year)
    {
        if (probationEndsOn is not { } endsOn || endsOn.Year < year)
        {
            return FullAnnualDays;
        }

        return endsOn.Year > year ? 0 : FullAnnualDays - endsOn.Month;
    }

    /// <summary>
    /// The enforced cap for one type in one year, or null when nothing is capped — which now
    /// means an Owner only, since nobody enforces a cap on an Owner's behalf.
    /// </summary>
    public static int? Entitled(LeaveType type, Employee employee, int year, LocalDate today)
    {
        if (employee.Role == EmployeeRole.Owner)
        {
            return null;
        }

        var over = employee.QuotaOverride(type);

        // Annual and Unpaid are mutually exclusive by probation status — see
        // CreateLeaveRequestHandler, which is where that eligibility is enforced. Sick and
        // Permission are filable throughout: someone on probation with flu still needs to be
        // able to record the absence, so their cap is the flat default, not a prorated one.
        if (type != LeaveType.Annual)
        {
            return over ?? DefaultDays(type);
        }

        if (employee.IsOnProbation(today))
        {
            return 0;
        }

        // An override is the whole-year figure — an Owner who typed a number meant that number,
        // not that number prorated again.
        return over ?? AnnualEntitlement(employee.ProbationEndsOn, year);
    }

    /// <summary>
    /// Workdays of this request that fall inside the given calendar year. A request spanning New
    /// Year charges each year the days actually taken in it, rather than dumping the lot on the
    /// year it started in.
    /// </summary>
    public static int WorkdaysInYear(LeaveRequest request, int year) =>
        LeaveRequest.Workdays(request.StartDate, request.EndDate).Count(date => date.Year == year);

    /// <summary>Approved workdays of one type falling in the given year.</summary>
    public static int UsedDays(IEnumerable<LeaveRequest> approved, LeaveType type, int year) =>
        approved.Where(request => request.Type == type).Sum(request => WorkdaysInYear(request, year));

    /// <summary>Approved workdays of every type falling in the given year.</summary>
    public static int UsedDaysAllTypes(IEnumerable<LeaveRequest> approved, int year) =>
        approved.Sum(request => WorkdaysInYear(request, year));
}
