using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Overtime;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using NodaTime;

namespace Erp.UseCases.Overtime.Common;

/// <summary>Assignments that claim punches (Pending, Approved, Expired) for one employee within an inclusive date range.</summary>
internal sealed class OvertimeOwningPunchesSpec : Specification<OvertimeAssignment>
{
    public OvertimeOwningPunchesSpec(EmployeeId employeeId, LocalDate from, LocalDate to)
    {
        Query.Where(a => a.EmployeeId == employeeId && a.Date >= from && a.Date <= to
                         && (a.Status == OvertimeStatus.Pending || a.Status == OvertimeStatus.Approved
                             || a.Status == OvertimeStatus.Expired));
        Query.AsNoTracking();
    }
}

/// <summary>
/// Pending or Approved overtime on the employee overlapping the range — what blocks filing
/// leave. A window that crosses midnight still counts on its start date only.
/// </summary>
internal sealed class LiveOvertimeInRangeSpec : Specification<OvertimeAssignment>
{
    public LiveOvertimeInRangeSpec(EmployeeId employeeId, LocalDate from, LocalDate to)
    {
        Query.Where(a => a.EmployeeId == employeeId && a.Date >= from && a.Date <= to
                         && (a.Status == OvertimeStatus.Pending || a.Status == OvertimeStatus.Approved));
        Query.AsNoTracking();
    }
}

internal sealed class OvertimeByIdSpec : SingleResultSpecification<OvertimeAssignment>
{
    public OvertimeByIdSpec(OvertimeAssignmentId id)
    {
        Query.Where(a => a.Id == id);
        Query.Include(a => a.Employee);
    }
}

/// <summary>
/// Everything the caller may read, newest date first. Staff see their own; a Manager their own
/// and their direct Staff's; an Owner everyone's.
/// </summary>
internal sealed class OvertimeListSpec : Specification<OvertimeAssignment>
{
    public OvertimeListSpec(
        Caller caller, LocalDate? from, LocalDate? to, EmployeeId? employeeId, OvertimeStatus? status, int? page, int? pageSize)
    {
        if (caller.Role == EmployeeRole.Staff)
        {
            Query.Where(a => a.EmployeeId == caller.EmployeeId);
        }
        else if (caller.Role == EmployeeRole.Manager)
        {
            Query.Where(a => a.EmployeeId == caller.EmployeeId || a.Employee!.ParentId == caller.EmployeeId);
        }

        if (from is { } f)

        {

            Query.Where(a => a.Date >= f);

        }

        if (to is { } t)
        {
            Query.Where(a => a.Date <= t);
        }

        if (employeeId is { } e)
        {
            Query.Where(a => a.EmployeeId == e);
        }

        if (status is { } s)
        {
            Query.Where(a => a.Status == s);
        }


        Query.Include(a => a.Employee);
        Query.OrderByDescending(a => a.Date).ThenBy(a => a.Employee!.FullName);
        if (page is { } p && pageSize is { } size)
        {
            Query.Skip((p - 1) * size).Take(size);
        }

        Query.AsNoTracking();
    }
}

/// <summary>Assignments of a Gaji Premi period that can carry pay or punches. <paramref name="tracked"/> for the close action.</summary>
internal sealed class PeriodOvertimeSpec : Specification<OvertimeAssignment>
{
    public PeriodOvertimeSpec(LocalDate from, LocalDate to, EmployeeId? employeeId = null, bool tracked = false)
    {
        Query.Where(a => a.Date >= from && a.Date <= to
                         && (a.Status == OvertimeStatus.Pending || a.Status == OvertimeStatus.Approved
                             || a.Status == OvertimeStatus.Expired));
        if (employeeId is { } e)
        {
            Query.Where(a => a.EmployeeId == e);
        }

        Query.Include(a => a.Employee);
        if (!tracked)
        {
            Query.AsNoTracking();
        }

    }
}

/// <summary>Anything before <paramref name="before"/> still awaiting its period's close — closing must go in order.</summary>
internal sealed class UnfrozenOvertimeBeforeSpec : Specification<OvertimeAssignment>
{
    public UnfrozenOvertimeBeforeSpec(LocalDate before)
    {
        Query.Where(a => a.Date < before && a.FrozenAtUtc == null
                         && (a.Status == OvertimeStatus.Pending || a.Status == OvertimeStatus.Approved
                             || a.Status == OvertimeStatus.Expired));
        Query.AsNoTracking();
    }
}

/// <summary>Punches for a set of employees in an instant window — one query for a whole page or period.</summary>
internal sealed class PunchesForEmployeesSpec : Specification<AttendanceLog>
{
    public PunchesForEmployeesSpec(IReadOnlyCollection<EmployeeId> employeeIds, Instant from, Instant to)
    {
        Query.Where(l => employeeIds.Contains(l.EmployeeId) && l.PunchedAtUtc >= from && l.PunchedAtUtc < to);
        Query.OrderBy(l => l.PunchedAtUtc);
        Query.AsNoTracking();
    }
}

internal sealed class ClosedPeriodsSpec : Specification<GajiPremiPeriod>
{
    public ClosedPeriodsSpec()
    {
        Query.OrderByDescending(p => p.StartDate);
        Query.AsNoTracking();
    }
}

internal sealed class ClosedPeriodByStartSpec : Specification<GajiPremiPeriod>
{
    public ClosedPeriodByStartSpec(LocalDate start)
    {
        Query.Where(p => p.StartDate == start);
        Query.AsNoTracking();
    }
}

internal sealed class CorrectionByIdSpec : SingleResultSpecification<OvertimeCorrectionRequest>
{
    public CorrectionByIdSpec(OvertimeCorrectionRequestId id)
    {
        Query.Where(c => c.Id == id);
        Query.Include(c => c.Employee);
    }
}

internal sealed class PendingCorrectionsSpec : Specification<OvertimeCorrectionRequest>
{
    public PendingCorrectionsSpec(OvertimeAssignmentId? assignmentId = null, LocalDate? from = null, LocalDate? to = null)
    {
        Query.Where(c => c.Status == OvertimeRequestStatus.Pending);
        if (assignmentId is { } a)
        {
            Query.Where(c => c.AssignmentId == a);
        }

        if (from is { } f)
        {
            Query.Where(c => c.WorkDate >= f);
        }

        if (to is { } t)
        {
            Query.Where(c => c.WorkDate <= t);
        }

    }
}

internal sealed class CorrectionListSpec : Specification<OvertimeCorrectionRequest>
{
    public CorrectionListSpec(Caller caller, OvertimeRequestStatus? status)
    {
        if (caller.Role == EmployeeRole.Staff)
        {
            Query.Where(c => c.EmployeeId == caller.EmployeeId);
        }
        else if (caller.Role == EmployeeRole.Manager)
        {
            Query.Where(c => c.EmployeeId == caller.EmployeeId || c.Employee!.ParentId == caller.EmployeeId);
        }

        if (status is { } s)

        {

            Query.Where(c => c.Status == s);

        }

        Query.Include(c => c.Employee);
        Query.OrderByDescending(c => c.RequestedAtUtc);
        Query.AsNoTracking();
    }
}

internal sealed class RapelByIdSpec : SingleResultSpecification<Rapel>
{
    public RapelByIdSpec(RapelId id)
    {
        Query.Where(r => r.Id == id);
        Query.Include(r => r.Employee);
    }
}

/// <summary>Rapel visible to the caller: Owner all, everyone else their own. Optionally one employee or one status.</summary>
internal sealed class RapelListSpec : Specification<Rapel>
{
    public RapelListSpec(Caller caller, EmployeeId? employeeId = null, OvertimeRequestStatus? status = null)
    {
        if (caller.Role != EmployeeRole.Owner)
        {
            Query.Where(r => r.EmployeeId == caller.EmployeeId);
        }

        if (employeeId is { } e)

        {

            Query.Where(r => r.EmployeeId == e);

        }

        if (status is { } s)
        {
            Query.Where(r => r.Status == s);
        }

        Query.Include(r => r.Employee);
        Query.OrderByDescending(r => r.RequestedAtUtc);
        Query.AsNoTracking();
    }
}

/// <summary>Approved rapel whose line lands on the payout of the period starting at <paramref name="payoutPeriodStart"/>.</summary>
internal sealed class RapelForPayoutSpec : Specification<Rapel>
{
    public RapelForPayoutSpec(LocalDate payoutPeriodStart)
    {
        Query.Where(r => r.Status == OvertimeRequestStatus.Approved && r.PayoutPeriodStart == payoutPeriodStart);
        Query.Include(r => r.Employee);
        Query.AsNoTracking();
    }
}

/// <summary>A pending or approved rapel on the assignment: one claim per overtime.</summary>
internal sealed class LiveRapelForAssignmentSpec : Specification<Rapel>
{
    public LiveRapelForAssignmentSpec(OvertimeAssignmentId assignmentId)
    {
        Query.Where(r => r.AssignmentId == assignmentId
            && (r.Status == OvertimeRequestStatus.Pending || r.Status == OvertimeRequestStatus.Approved));
    }
}
