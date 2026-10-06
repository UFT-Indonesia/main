using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Common;
using Erp.UseCases.Overtime.Common;
using NodaTime;

namespace Erp.UseCases.Attendance.ListAttendanceDays;

/// <summary>
/// Assembles the calendar: every date in the period, and on each one everyone who was employed
/// that day — whether or not they left a trace. Shared by the list endpoint and the CSV export
/// so the file can never disagree with the screen it was exported from.
/// </summary>
public static class AttendanceCalendar
{
    /// <summary>
    /// ponytail: one pass over the whole period, capped at a quarter. Split into a summary plus
    /// a per-date fetch if headcount ever makes the payload hurt.
    /// </summary>
    public const int MaxPeriodDays = 92;

    /// <summary>
    /// Loads the population and the materialized days, then walks the period newest date first.
    /// Filters have already been compiled against <see cref="AttendanceCalendarFilterFields"/>.
    /// </summary>
    public static async Task<IReadOnlyList<AttendanceCalendarDateResult>> BuildAsync(
        LocalDate from,
        LocalDate to,
        IReadOnlyList<System.Linq.Expressions.Expression<Func<Employee, bool>>> filters,
        Caller caller,
        IReadRepository<AttendanceDay> attendanceDays,
        IReadRepository<Employee> employees,
        IReadRepository<OvertimeAssignment> overtime,
        IReadRepository<AttendanceLog> logs,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        // Ordered by full name, so the per-date sort below only has to be stable by status rank.
        var population = await employees.ListAsync(
            new AttendanceCalendarEmployeesSpec(from, to, filters, caller), ct);

        var visibleIds = population.Select(employee => employee.Id).ToHashSet();
        var materialized = await attendanceDays.ListAsync(new AttendanceDaysInRangeSpec(from, to, visibleIds), ct);
        var dayByKey = materialized
            .ToDictionary(day => (day.EmployeeId, day.CalendarDate));

        // OT assignments claim punches the regular day no longer holds, so they are read on their
        // own and laid over the day rows. Hours come from the same calculator payroll uses.
        var assignments = (await overtime.ListAsync(new PeriodOvertimeSpec(from, to), ct))
            .Where(a => visibleIds.Contains(a.EmployeeId))
            .ToList();
        var punches = await OvertimeEvaluator.LoadPunchesAsync(assignments, logs, policy, ct);
        var overtimeByKey = assignments.ToDictionary(
            a => (a.EmployeeId, a.Date),
            a => OvertimeInfo.From(a, OvertimeEvaluator.Evaluate(a, punches, policy, [])));

        var now = clock.GetCurrentInstant();
        var today = now.InZone(policy.TimeZone).Date;

        // The instant today stops being provisional. Until it passes, statuses still move on
        // their own — everyone still at work reads Incomplete — so today reports arrivals
        // instead of outcomes. Both halves already live on the policy.
        var shiftClosesAt = today
            .At(policy.ShiftEnd)
            .InZoneLeniently(policy.TimeZone)
            .ToInstant()
            .Plus(Duration.FromMinutes(policy.ClockOutGraceMinutes));

        var dates = new List<AttendanceCalendarDateResult>();
        for (var date = to; date >= from; date = date.PlusDays(-1))
        {
            // Weekends and declared holidays alike — the same rule leave charging uses.
            var isWorkday = policy.IsWorkday(date);
            var isFuture = date > today;
            var isInProgress = date == today && now < shiftClosesAt;

            dates.Add(new AttendanceCalendarDateResult
            {
                Date = date.ToDateOnly(),
                IsWorkday = isWorkday,
                IsFuture = isFuture,
                IsInProgress = isInProgress,
                Employees = BuildEmployees(
                    population, dayByKey, overtimeByKey, date, isWorkday, isFuture, isInProgress, caller),
            });
        }

        return dates;
    }

    private static List<AttendanceDayListItemResult> BuildEmployees(
        IReadOnlyList<Employee> population,
        IReadOnlyDictionary<(EmployeeId, LocalDate), AttendanceDay> dayByKey,
        IReadOnlyDictionary<(EmployeeId, LocalDate), OvertimeInfo> overtimeByKey,
        LocalDate date,
        bool isWorkday,
        bool isFuture,
        bool isInProgress,
        Caller caller)
    {
        var items = new List<AttendanceDayListItemResult>();

        foreach (var employee in population)
        {
            var day = dayByKey.GetValueOrDefault((employee.Id, date));
            var ot = overtimeByKey.GetValueOrDefault((employee.Id, date));

            if (day is null)
            {
                // A day off whose punches all went to an OT assignment has no regular row, but
                // the person worked — report it rather than hiding the day.
                if (!isWorkday && ot is not null)
                {
                    items.Add(Missing(employee, date, isWorkday, isFuture, isInProgress, caller, ot));
                    continue;
                }

                // Somebody hired next month is not absent today, and neither is somebody who
                // left last year. Employment dates rather than EmployeeStatus, which holds only
                // today's value and would shrink every historical denominator.
                var employedThatDay =
                    (employee.HireDate is not { } hired || hired <= date)
                    && (employee.TerminationDate is not { } left || left > date);

                // Nobody is absent from a day nobody works.
                if (!employedThatDay || !isWorkday)
                {
                    continue;
                }

                items.Add(Missing(employee, date, isWorkday, isFuture, isInProgress, caller, ot));
                continue;
            }

            items.Add(Present(employee, day, isWorkday, isInProgress, caller, ot));
        }

        // Decision 13: problems first, alphabetical inside each group. OrderBy is stable and the
        // population arrived name-ordered, so the second key is already applied.
        return items
            .OrderBy(item => AttendanceCalendarStatus.Rank(item.Status))
            .ToList();
    }

    private static AttendanceDayListItemResult Missing(
        Employee employee,
        LocalDate date,
        bool isWorkday,
        bool isFuture,
        bool isInProgress,
        Caller caller,
        OvertimeInfo? ot) => new()
        {
            EmployeeId = employee.Id.Value,
            EmployeeFullName = employee.FullName,
            Date = date.ToDateOnly(),
            Status = !isWorkday
                ? AttendanceCalendarStatus.Overtime
                : isFuture
                ? AttendanceCalendarStatus.Upcoming
                : isInProgress
                    ? AttendanceCalendarStatus.NotInYet
                    : AttendanceCalendarStatus.Absent,
            CanWrite = AttendanceRules.CanWriteFor(caller, employee),
            OvertimeStatus = ot?.Status,
            OvertimeStart = ot?.Start,
            OvertimeEnd = ot?.End,
            OvertimeEndsNextDay = ot?.EndsNextDay ?? false,
            OvertimeHours = ot?.Hours,
            OvertimeIncomplete = ot?.Incomplete ?? false,
        };

    private static AttendanceDayListItemResult Present(
        Employee employee,
        AttendanceDay day,
        bool isWorkday,
        bool isInProgress,
        Caller caller,
        OvertimeInfo? ot) => new()
        {
            EmployeeId = day.EmployeeId.Value,
            EmployeeFullName = employee.FullName,
            Date = day.CalendarDate.ToDateOnly(),
            TapInUtc = day.TapInUtc?.ToDateTimeOffset(),
            TapOutUtc = day.TapOutUtc?.ToDateTimeOffset(),

            // A day off has no shift to complete, so punches on it are reported, never judged.
            // While today is unfinished, a punch means "here", not "done" — the second punch has
            // not been missed yet. A punchless row on such a day can only have come from leave.
            Status = !isWorkday && day.TapInUtc is not null
                ? AttendanceCalendarStatus.WorkedOnDayOff
                : isInProgress
                ? day.TapInUtc is not null
                    ? AttendanceCalendarStatus.ClockedIn
                    : AttendanceCalendarStatus.OnLeave
                : day.Status.ToString(),

            LeaveType = day.LeaveRequest?.Type.ToString() ?? string.Empty,
            LeaveStartDate = day.LeaveRequest?.StartDate.ToDateOnly(),
            LeaveEndDate = day.LeaveRequest?.EndDate.ToDateOnly(),
            LeaveWorkdayCount = day.LeaveRequest?.WorkdayCount,
            LeaveReason = day.LeaveRequest?.Reason,
            LeaveRequestedAtUtc = day.LeaveRequest?.RequestedAtUtc.ToDateTimeOffset(),
            LeaveDecidedByName = day.LeaveRequest?.DecidedByName,
            LeaveDecidedAtUtc = day.LeaveRequest?.DecidedAtUtc?.ToDateTimeOffset(),
            LeaveRequestId = day.LeaveRequest?.Id.Value,
            LeaveAttachmentFileName = day.LeaveRequest?.Attachment?.FileName,
            CanWrite = AttendanceRules.CanWriteFor(caller, employee),
            OvertimeStatus = ot?.Status,
            OvertimeStart = ot?.Start,
            OvertimeEnd = ot?.End,
            OvertimeEndsNextDay = ot?.EndsNextDay ?? false,
            OvertimeHours = ot?.Hours,
            OvertimeIncomplete = ot?.Incomplete ?? false,
        };
}

/// <summary>What the calendar shows of one OT assignment: window, status, counted hours — never pay.</summary>
internal sealed record OvertimeInfo(
    string Status, TimeOnly Start, TimeOnly End, bool EndsNextDay, int Hours, bool Incomplete)
{
    internal static OvertimeInfo From(OvertimeAssignment a, OvertimeEvaluation e) => new(
        a.Status.ToString(), a.StartTime.ToTimeOnly(), a.EndTime.ToTimeOnly(), a.EndDate != a.Date, e.Hours,
        a.Status == OvertimeStatus.Approved && e.Count.Incomplete);
}
