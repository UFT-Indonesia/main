using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using NodaTime;

namespace Erp.Infrastructure.Attendance;

/// <summary>
/// Hangfire recurring job: points every employee's <see cref="EmployeeStatus.OnLeave"/> flag
/// at whether approved leave covers today. Approving and cancelling already reconcile the one
/// employee they touch, but leave that simply runs out fires no event — without this, the
/// badge would stay lit until the next decision happened to move it.
/// </summary>
public sealed class SyncEmployeeLeaveStatusJob
{
    private readonly IRepository<Employee> _employees;
    private readonly IReadRepository<LeaveRequest> _leaveRequests;
    private readonly AttendanceDayPolicy _policy;
    private readonly IClock _clock;

    public SyncEmployeeLeaveStatusJob(
        IRepository<Employee> employees,
        IReadRepository<LeaveRequest> leaveRequests,
        AttendanceDayPolicy policy,
        IClock clock)
    {
        _employees = employees;
        _leaveRequests = leaveRequests;
        _policy = policy;
        _clock = clock;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var today = AttendanceDayRecomputeService.CalendarDateOf(_clock.GetCurrentInstant(), _policy);

        // Only employees who could change: someone the badge is currently on, or someone an
        // approved request covers today. Everyone else is already correct.
        var candidates = await _employees.ListAsync(new EmployeesOnLeaveSpec(), ct);
        var covered = await _leaveRequests.ListAsync(new ApprovedLeaveCoveringDateSpec(today), ct);

        var employeeIds = candidates.Select(employee => employee.Id)
            .Concat(covered.Select(request => request.EmployeeId))
            .ToHashSet();

        foreach (var employeeId in employeeIds)
        {
            await LeaveAttendanceSync.ReconcileEmployeeStatusAsync(
                employeeId, today, _employees, _leaveRequests, ct);
        }
    }
}

/// <summary>Employees the badge is currently on — the ones whose leave may have ended.</summary>
internal sealed class EmployeesOnLeaveSpec : Specification<Employee>
{
    public EmployeesOnLeaveSpec()
    {
        Query.Where(employee => employee.Status == EmployeeStatus.OnLeave);
        Query.AsNoTracking();
    }
}

/// <summary>Approved requests spanning the given date, across all employees.</summary>
internal sealed class ApprovedLeaveCoveringDateSpec : Specification<LeaveRequest>
{
    public ApprovedLeaveCoveringDateSpec(LocalDate date)
    {
        Query.Where(request => request.Status == LeaveRequestStatus.Approved
                               && request.StartDate <= date
                               && date <= request.EndDate);
        Query.AsNoTracking();
    }
}
