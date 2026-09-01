using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Leave.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Leave.Common;
using NodaTime;

namespace Erp.UseCases.Attendance.Common;

/// <summary>
/// Keeps attendance in step with leave decisions: the materialized day rows that make an
/// approved absence visible in the table and the export, and the employee-level
/// <see cref="EmployeeStatus.OnLeave"/> badge.
/// </summary>
public static class LeaveAttendanceSync
{
    /// <summary>
    /// Materializes a single <see cref="AttendanceDayStatus.OnLeave"/> row, on the first
    /// workday the leave covers. One decision produces one row: a ten-day leave used to fill
    /// the table with eight near-identical "Cuti" entries, and the detail dialog on the one
    /// row spells out the whole range anyway.
    /// </summary>
    /// <remarks>
    /// Weekends are skipped, so the first row is the first *workday*, not necessarily
    /// <paramref name="startDate"/>. If that day already has a row it is left alone — a real
    /// punch outranks the leave, the day is already in the table, and the unique
    /// (employee, date) index would reject the insert anyway.
    /// </remarks>
    public static async Task MaterializeAsync(
        LeaveRequestId leaveRequestId,
        EmployeeId employeeId,
        LocalDate startDate,
        LocalDate endDate,
        IRepository<AttendanceDay> attendanceDays,
        CancellationToken ct)
    {
        var firstWorkday = LeaveRequest.Workdays(startDate, endDate)
            .Select(date => (LocalDate?)date)
            .FirstOrDefault();

        if (firstWorkday is not { } workday)
        {
            return;
        }

        var existing = await attendanceDays.ListAsync(
            new AttendanceDayDatesInRangeSpec(employeeId, workday, workday), ct);

        if (existing.Count > 0)
        {
            return;
        }

        await attendanceDays.AddAsync(
            AttendanceDay.CreateForLeave(employeeId, workday, leaveRequestId), ct);
    }

    /// <summary>
    /// Undoes <see cref="MaterializeAsync"/> for a cancelled request. A row the leave put
    /// there on its own is deleted, restoring the "no punches, no row" shape every other
    /// untouched day has. A row that has since collected punches is real attendance and
    /// survives — it only loses the link to the leave that no longer applies.
    /// </summary>
    public static async Task ReleaseAsync(
        LeaveRequestId leaveRequestId,
        IRepository<AttendanceDay> attendanceDays,
        CancellationToken ct)
    {
        var linked = await attendanceDays.ListAsync(
            new AttendanceDaysForLeaveRequestSpec(leaveRequestId), ct);

        foreach (var day in linked)
        {
            if (day.TapInUtc is null)
            {
                await attendanceDays.DeleteAsync(day, ct);
                continue;
            }

            day.ClearLeaveLink();
            await attendanceDays.UpdateAsync(day, ct);
        }
    }

    /// <summary>
    /// Drops the leave-only days an employee no longer has, past their last working day.
    /// Their approved request keeps its original dates — the decision was genuinely made and
    /// stays on the record — but the days it materialized past termination describe someone
    /// who is no longer employed, and would otherwise reach the table and the payroll export.
    /// Days on or before the termination date are real history and stay, as does anything
    /// carrying a punch.
    /// </summary>
    public static async Task DropLeaveDaysAfterAsync(
        EmployeeId employeeId,
        LocalDate lastWorkingDay,
        IRepository<AttendanceDay> attendanceDays,
        CancellationToken ct)
    {
        var stale = await attendanceDays.ListAsync(
            new LeaveOnlyDaysAfterSpec(employeeId, lastWorkingDay), ct);

        foreach (var day in stale)
        {
            await attendanceDays.DeleteAsync(day, ct);
        }
    }

    /// <summary>
    /// Points <see cref="Employee.Status"/> at whether approved leave covers <paramref name="today"/>.
    /// Recomputed from the leave table rather than toggled per decision, so overlapping and
    /// back-to-back requests can't leave the badge stuck on — and so the daily job can run the
    /// exact same check to end leave that simply ran out.
    /// </summary>
    public static async Task ReconcileEmployeeStatusAsync(
        EmployeeId employeeId,
        LocalDate today,
        IRepository<Employee> employees,
        IReadRepository<LeaveRequest> leaveRequests,
        CancellationToken ct)
    {
        var employee = await employees.GetByIdAsync(employeeId, ct);
        if (employee is null || employee.Status == EmployeeStatus.Terminated)
        {
            return;
        }

        var onLeave = await leaveRequests.AnyAsync(
            new ApprovedLeaveOverlappingSpec(employeeId, today, today), ct);

        if ((employee.Status == EmployeeStatus.OnLeave) == onLeave)
        {
            return;
        }

        employee.SetOnLeave(onLeave);
        await employees.UpdateAsync(employee, ct);
    }
}

public static class LeaveRequestApprovedHandler
{
    public static async Task Handle(
        LeaveRequestApproved message,
        IRepository<AttendanceDay> attendanceDays,
        IRepository<Employee> employees,
        IReadRepository<LeaveRequest> leaveRequests,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        var employeeId = new EmployeeId(message.EmployeeId);

        await LeaveAttendanceSync.MaterializeAsync(
            new LeaveRequestId(message.LeaveRequestId),
            employeeId,
            message.StartDate,
            message.EndDate,
            attendanceDays,
            ct);

        await LeaveAttendanceSync.ReconcileEmployeeStatusAsync(
            employeeId,
            AttendanceDayRecomputeService.CalendarDateOf(clock.GetCurrentInstant(), policy),
            employees,
            leaveRequests,
            ct);
    }
}

public static class EmployeeTerminatedAttendanceHandler
{
    public static Task Handle(
        EmployeeTerminated message,
        IRepository<AttendanceDay> attendanceDays,
        CancellationToken ct) =>
        LeaveAttendanceSync.DropLeaveDaysAfterAsync(
            new EmployeeId(message.EmployeeId), message.TerminationDate, attendanceDays, ct);
}

public static class LeaveRequestCancelledHandler
{
    public static async Task Handle(
        LeaveRequestCancelled message,
        IRepository<AttendanceDay> attendanceDays,
        IRepository<Employee> employees,
        IReadRepository<LeaveRequest> leaveRequests,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        await LeaveAttendanceSync.ReleaseAsync(
            new LeaveRequestId(message.LeaveRequestId), attendanceDays, ct);

        await LeaveAttendanceSync.ReconcileEmployeeStatusAsync(
            new EmployeeId(message.EmployeeId),
            AttendanceDayRecomputeService.CalendarDateOf(clock.GetCurrentInstant(), policy),
            employees,
            leaveRequests,
            ct);
    }
}
