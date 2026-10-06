using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Overtime.Common;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Erp.UseCases.Overtime.DecideOvertimeAssignment;

// Lifecycle violations (already decided, period closed, has punches) throw DomainException and
// bubble to the global handler as 400s, same as leave.

public sealed record ApproveOvertimeCommand(Guid Id, Caller Caller);

public sealed record RejectOvertimeCommand(Guid Id, Caller Caller, string? Note);

public sealed record CancelOvertimeCommand(Guid Id, Caller Caller, string? Note);

public sealed record EditOvertimeEndCommand(Guid Id, TimeOnly EndTime, Caller Caller);

public static class ApproveOvertimeHandler
{
    public static Task<Result<OvertimeAssignmentResult>> Handle(
        ApproveOvertimeCommand command, IRepository<OvertimeAssignment> assignments,
        IReadRepository<OvertimeAssignment> assignmentsRead, IReadRepository<AttendanceLog> logs,
        IRepository<AttendanceDay> days, AttendanceDayPolicy policy, IOptions<OvertimeOptions> options,
        IClock clock, CancellationToken ct) =>
        DecideOvertimeService.DecideAsync(
            command.Id, command.Caller, (caller, _) => OvertimeRules.CanDecide(caller), recompute: false,
            (a, _, _, now) => a.Approve(command.Caller.UserId, command.Caller.Name, now),
            assignments, assignmentsRead, logs, days, policy, options, clock, ct);
}

public static class RejectOvertimeHandler
{
    public static Task<Result<OvertimeAssignmentResult>> Handle(
        RejectOvertimeCommand command, IRepository<OvertimeAssignment> assignments,
        IReadRepository<OvertimeAssignment> assignmentsRead, IReadRepository<AttendanceLog> logs,
        IRepository<AttendanceDay> days, AttendanceDayPolicy policy, IOptions<OvertimeOptions> options,
        IClock clock, CancellationToken ct) =>
        DecideOvertimeService.DecideAsync(
            command.Id, command.Caller, (caller, _) => OvertimeRules.CanDecide(caller),
            // Rejected overtime stops owning its punches, so the regular day gets them back.
            recompute: true,
            (a, _, _, now) => a.Reject(command.Caller.UserId, command.Caller.Name, now, command.Note),
            assignments, assignmentsRead, logs, days, policy, options, clock, ct);
}

public static class CancelOvertimeHandler
{
    public static Task<Result<OvertimeAssignmentResult>> Handle(
        CancelOvertimeCommand command, IRepository<OvertimeAssignment> assignments,
        IReadRepository<OvertimeAssignment> assignmentsRead, IReadRepository<AttendanceLog> logs,
        IRepository<AttendanceDay> days, AttendanceDayPolicy policy, IOptions<OvertimeOptions> options,
        IClock clock, CancellationToken ct) =>
        DecideOvertimeService.DecideAsync(
            command.Id, command.Caller, OvertimeRules.CanAssign, recompute: true,
            (a, _, hasPunches, now) => a.Cancel(command.Caller.UserId, command.Caller.Name, now, command.Note, hasPunches),
            assignments, assignmentsRead, logs, days, policy, options, clock, ct);
}

public static class EditOvertimeEndHandler
{
    public static Task<Result<OvertimeAssignmentResult>> Handle(
        EditOvertimeEndCommand command, IRepository<OvertimeAssignment> assignments,
        IReadRepository<OvertimeAssignment> assignmentsRead, IReadRepository<AttendanceLog> logs,
        IRepository<AttendanceDay> days, AttendanceDayPolicy policy, IOptions<OvertimeOptions> options,
        IClock clock, CancellationToken ct) =>
        DecideOvertimeService.DecideAsync(
            command.Id, command.Caller, OvertimeRules.CanAssign, recompute: false,
            // A Manager's edit goes back to the Owner; an Owner's stands.
            (a, _, hasPunches, _) => a.ChangeEnd(
                LocalTime.FromTimeOnly(command.EndTime), hasPunches, returnToPending: command.Caller.Role != EmployeeRole.Owner),
            assignments, assignmentsRead, logs, days, policy, options, clock, ct);
}

internal static class DecideOvertimeService
{
    internal static async Task<Result<OvertimeAssignmentResult>> DecideAsync(
        Guid id, Caller caller, Func<Caller, Employee, bool> permitted, bool recompute,
        Action<OvertimeAssignment, Employee, bool, Instant> act,
        IRepository<OvertimeAssignment> assignments, IReadRepository<OvertimeAssignment> assignmentsRead,
        IReadRepository<AttendanceLog> logs, IRepository<AttendanceDay> days, AttendanceDayPolicy policy,
        IOptions<OvertimeOptions> options, IClock clock, CancellationToken ct)
    {
        var assignment = await assignments.FirstOrDefaultAsync(new OvertimeByIdSpec(new OvertimeAssignmentId(id)), ct);
        if (assignment?.Employee is not { } subject)
        {
            return new Result<OvertimeAssignmentResult>.NotFound("Overtime was not found.");
        }

        if (!permitted(caller, subject))
        {
            return new Result<OvertimeAssignmentResult>.Error(
                ResultErrors.Forbidden, "You cannot change this overtime.");
        }

        var punches = await OvertimeEvaluator.LoadPunchesAsync([assignment], logs, policy, ct);
        var hasPunches = OvertimeCalculator.OwnedPunches(assignment, punches, policy).Count > 0;

        act(assignment, subject, hasPunches, clock.GetCurrentInstant());
        await assignments.UpdateAsync(assignment, ct);

        if (recompute)
        {
            await OvertimeEvaluator.RecomputeAroundAsync(assignment, logs, days, assignmentsRead, policy, ct);
        }

        return new Result<OvertimeAssignmentResult>.Success(
            await OvertimeMapper.DescribeAsync(assignment, subject, caller, logs, policy, options, ct));
    }
}
