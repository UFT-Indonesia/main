using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using Erp.UseCases.Leave.Common;
using NodaTime;

namespace Erp.UseCases.Payroll.Common;

/// <summary>A leave day with the salary and daily rate it is priced at. <see cref="Exact"/> is unrounded.</summary>
internal sealed record PricedDay(Employee Employee, LeaveDeductionDay Day, decimal Salary, decimal Rate)
{
    internal decimal Exact => Day.CutDays * Rate;
}

/// <summary>
/// Loads what <see cref="LeaveDeductionCalculator"/> needs and feeds it. The calculator stays pure;
/// everything that touches a repository lives here, once, so the leave guard, the balance and the
/// payroll page all see the same cut days.
/// </summary>
internal static class LeaveDeductionEngine
{
    internal static Instant EffectiveAt(LeaveRequest request)
    {
        var decided = request.DecidedAtUtc ?? request.RequestedAtUtc;
        return request.EditedAtUtc is { } edited && edited > decided ? edited : decided;
    }

    internal static LeaveDeductionRequest ToRequest(LeaveRequest request, AttendanceDayPolicy policy) => new(
        request.Id.Value, request.Type, request.StartDate, request.EndDate, request.ChargePerWorkday(policy),
        EffectiveAt(request));

    internal static IReadOnlyDictionary<(Guid, LocalDate), (decimal, decimal)> FrozenLabels(
        IEnumerable<LeaveDeductionLine> lines) =>
        // A superseded line is history (what was paid); only live ones keep a day's label.
        lines.Where(l => l.IsActive).ToDictionary(l => (l.LeaveRequestId, l.Date), l => (l.FreeDays, l.CutDays));

    /// <summary>Every workday of an employee's approved leave split into free and cut, in approval order.</summary>
    internal static IReadOnlyList<LeaveDeductionDay> Allocate(
        Employee employee,
        IEnumerable<LeaveDeductionRequest> requests,
        IEnumerable<LeaveDeductionLine> frozenLines,
        AttendanceDayPolicy policy,
        LocalDate today) =>
        LeaveDeductionCalculator.Allocate(
            requests,
            (type, year) => LeaveQuota.Entitled(type, employee, year, today),
            policy,
            FrozenLabels(frozenLines));

    internal static async Task<IReadOnlySet<LocalDate>> ClosedMonthsAsync(
        IReadRepositoryBase<LeaveDeductionMonth> months, CancellationToken ct) =>
        (await months.ListAsync(new ClosedDeductionMonthsSpec(), ct)).Select(m => m.Month).ToHashSet();

    /// <summary>
    /// The days of a request that has not been approved yet (or is being edited), as they would fall
    /// if it were approved now: it queues last, so it spends whatever cap is left. Drives the form
    /// warning, the over-quota count on the request, and the closed-month check.
    /// </summary>
    internal static async Task<IReadOnlyList<LeaveDeductionDay>> CandidateDaysAsync(
        Employee employee,
        LeaveType type,
        LocalDate start,
        LocalDate end,
        decimal chargePerWorkday,
        Instant now,
        AttendanceDayPolicy policy,
        LocalDate today,
        IReadRepositoryBase<LeaveRequest> leaveRequests,
        IReadRepositoryBase<LeaveDeductionLine> lines,
        CancellationToken ct,
        Erp.SharedKernel.Identity.LeaveRequestId? excludeRequestId = null)
    {
        var fromYear = start.Year;
        var toYear = end.Year;
        var approved = await leaveRequests.ListAsync(
            new ApprovedLeaveForYearSpec([employee.Id], fromYear, toYear, excludeRequestId), ct);
        var frozen = await lines.ListAsync(
            new DeductionLinesForEmployeesSpec([employee.Id.Value], fromYear, toYear), ct);

        // Largest possible id: if an approved request shares the exact instant, the candidate still queues last.
        var candidateId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var requests = approved.Select(r => ToRequest(r, policy))
            .Append(new LeaveDeductionRequest(candidateId, type, start, end, chargePerWorkday, now));

        return Allocate(employee, requests, frozen, policy, today).Where(d => d.RequestId == candidateId).ToList();
    }

    /// <summary>
    /// Free and cut leave days of every employee falling inside an open month, priced at the salary in
    /// effect on each day. Owners are never cut and are left out.
    /// </summary>
    internal static async Task<IReadOnlyList<PricedDay>> PriceOpenMonthAsync(
        LocalDate month,
        PayrollSettings settings,
        IReadRepositoryBase<LeaveRequest> leaveRequests,
        IReadRepositoryBase<Employee> employees,
        IReadRepositoryBase<EmployeeSalaryHistory> salaries,
        IReadRepositoryBase<LeaveDeductionLine> lines,
        AttendanceDayPolicy policy,
        LocalDate today,
        CancellationToken ct)
    {
        var end = LeaveDeductionMonth.EndOf(month);
        var approved = await leaveRequests.ListAsync(new ApprovedLeaveInYearSpec(month.Year), ct);
        var people = (await employees.ListAsync(
                new EmployeesByIdsSpec(approved.Select(r => r.EmployeeId).Distinct().ToList()), ct))
            .Where(e => e.Role != EmployeeRole.Owner)
            .ToList();
        var ids = people.Select(p => p.Id.Value).ToList();
        var history = (await salaries.ListAsync(new SalaryHistoryOfEmployeesSpec(ids), ct))
            .ToLookup(h => h.EmployeeId);
        var frozen = (await lines.ListAsync(new DeductionLinesForEmployeesSpec(ids, month.Year, month.Year), ct))
            .ToLookup(l => l.EmployeeId);

        var priced = new List<PricedDay>();
        foreach (var person in people)
        {
            var mine = approved.Where(r => r.EmployeeId == person.Id).Select(r => ToRequest(r, policy));
            var points = history[person.Id.Value].Select(h => new SalaryPoint(h.EffectiveFrom, h.Amount)).ToList();

            foreach (var day in Allocate(person, mine, frozen[person.Id.Value], policy, today)
                         .Where(d => d.Date >= month && d.Date <= end))
            {
                var salary = LeaveDeductionCalculator.SalaryOn(points, day.Date, person.MonthlyWage.Amount);
                var rate = LeaveDeductionCalculator.DailyRate(salary, settings.Divisor, person.LeaveDeductionException);
                priced.Add(new PricedDay(person, day, salary, rate));
            }
        }

        return priced;
    }
}
