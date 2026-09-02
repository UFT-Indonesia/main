using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using NodaTime;

namespace Erp.UseCases.Leave.EditLeaveRequest;

/// <summary>
/// Corrects the dates of a request already in flight, rather than the cancel-and-refile the
/// aggregate was originally built around.
/// <para>
/// Authority is <see cref="LeaveRules.CanDecideFor"/> verbatim: whoever had the standing to
/// approve it has the standing to fix it — an Owner for anyone, a Manager for their own direct
/// Staff. Nothing new to keep in sync with the approve path.
/// </para>
/// </summary>
public static class EditLeaveRequestHandler
{
    public static async Task<Result<LeaveRequestResult>> Handle(
        EditLeaveRequestCommand command,
        IRepository<LeaveRequest> leaveRequests,
        IRepository<Employee> employees,
        IRepository<AttendanceDay> attendanceDays,
        AttendanceDayPolicy policy,
        // ReconcileEmployeeStatusAsync wants the read interface specifically; IRepository and
        // IReadRepository are siblings here, not parent and child.
        IReadRepository<LeaveRequest> leaveRequestsRead,
        IClock clock,
        CancellationToken ct)
    {
        var requestId = new LeaveRequestId(command.LeaveRequestId);
        var request = await leaveRequests.FirstOrDefaultAsync(new LeaveRequestByIdSpec(requestId), ct);
        if (request is null)
        {
            return new Result<LeaveRequestResult>.NotFound("Leave request was not found.");
        }

        // Loaded explicitly rather than off the navigation: authority hinges on the subject's
        // current role and reporting line, so it should not depend on an Include staying put.
        var subject = await employees.GetByIdAsync(request.EmployeeId, ct);
        if (subject is null)
        {
            return new Result<LeaveRequestResult>.NotFound("The employee this request belongs to was not found.");
        }

        if (!LeaveRules.CanDecideFor(command.Caller, subject))
        {
            return new Result<LeaveRequestResult>.Error(
                ResultErrors.Forbidden, "You cannot edit this leave request.");
        }

        var startDate = LocalDate.FromDateOnly(command.StartDate);
        var endDate = LocalDate.FromDateOnly(command.EndDate);
        var today = DisplayZone.Today(clock);

        // Every gate a new request clears, the edited shape clears too — otherwise editing is a
        // way around all of them. The one difference is excluding this request from both checks:
        // an approved request would otherwise be found overlapping itself, and its own charge
        // would count as quota already spent against its own new dates.
        if (command.StartHour is { } startHour && command.EndHour is { } endHour
            && endHour - startHour > policy.MaxIzinHours)
        {
            return new Result<LeaveRequestResult>.Error(
                "leave.izin_hours_exceeded",
                $"Izin cannot exceed {policy.MaxIzinHours} hour(s); this request spans {endHour - startHour}.");
        }

        var candidateWindow = LeaveRequest.OccupiedWindow(
            command.HalfDay, command.HalfDayPeriod, command.StartHour, command.EndHour, policy);
        var overlapping = await leaveRequests.ListAsync(
            new ApprovedLeaveOverlappingSpec(request.EmployeeId, startDate, endDate, requestId), ct);
        if (overlapping.Any(existing =>
            LeaveRequest.WindowsIntersect(existing.OccupiedWindow(policy), candidateWindow)))
        {
            return new Result<LeaveRequestResult>.Error(
                "leave.overlaps_approved", "The requested dates overlap an already approved leave.");
        }

        var overQuota = await LeaveQuotaGuard.CheckAsync(
            subject, request.Type, startDate, endDate,
            command.HalfDay, command.StartHour, command.EndHour, policy,
            leaveRequests, today, ct, excludeRequestId: requestId);
        if (overQuota is { } violation)
        {
            return new Result<LeaveRequestResult>.Error(violation.Code, violation.Message);
        }

        var wasApproved = request.Status == LeaveRequestStatus.Approved;

        try
        {
            var now = clock.GetCurrentInstant();
            request.Edit(
                startDate,
                endDate,
                command.HalfDay,
                command.HalfDayPeriod,
                command.StartHour,
                command.EndHour,
                command.Caller.UserId,
                command.Caller.Name,
                now);

            // An Owner editing a pending request decides it in the same act — nobody outranks
            // them, so there is nobody left for it to wait on. Deliberately NOT extended to a
            // Manager: a Manager who filed for their own Staff is barred from approving it
            // (LeaveRules.IsRequester), and letting an edit approve it would be a way around that.
            if (!wasApproved && command.Caller.Role == EmployeeRole.Owner)
            {
                request.Approve(command.Caller.UserId, command.Caller.Name, now);
            }
        }
        catch (DomainException ex)
        {
            return new Result<LeaveRequestResult>.Error(ex.Code ?? "leave.validation", ex.Message);
        }

        await leaveRequests.UpdateAsync(request, ct);

        // The dates this was materialized against have moved. Release what the old ones put in
        // attendance, then materialize the new ones — the same two operations cancel and approve
        // already use, run back to back.
        if (request.Status == LeaveRequestStatus.Approved)
        {
            await LeaveAttendanceSync.ReleaseAsync(requestId, attendanceDays, ct);
            await LeaveAttendanceSync.MaterializeAsync(
                requestId,
                request.EmployeeId,
                request.StartDate,
                request.EndDate,
                request.HalfDay || request.StartHour is not null,
                attendanceDays,
                ct);
        }

        await LeaveAttendanceSync.ReconcileEmployeeStatusAsync(
            request.EmployeeId,
            AttendanceDayRecomputeService.CalendarDateOf(clock.GetCurrentInstant(), policy),
            employees,
            leaveRequestsRead,
            ct);

        var (canDecide, canCancel, canEdit) = LeaveRequestResult.PermissionsFor(command.Caller, request, subject);
        return new Result<LeaveRequestResult>.Success(
            LeaveRequestResult.From(
                request,
                policy,
                // Single-request responses do not run the yearly rollup query.
                approvedWorkdaysThisYear: null,
                employeeFullName: subject.FullName,
                canDecide: canDecide,
                canCancel: canCancel,
                canEdit: canEdit,
                // Standing to edit implies standing to read what was edited.
                canReadDetails: true));
    }
}
