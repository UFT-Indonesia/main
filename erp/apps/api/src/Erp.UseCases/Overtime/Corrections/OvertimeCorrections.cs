using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.GetLeaveAttachment;
using Erp.UseCases.Overtime.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Overtime.Corrections;

/// <summary>
/// <paramref name="Time"/> is a time of day: before 05:00 it means the small hours after the
/// work date, since every overtime day runs 05:00 → 05:00. <paramref name="Attachment"/> arrives
/// already stored, like leave's.
/// </summary>
public sealed record CreateOvertimeCorrectionCommand(
    Guid AssignmentId, string Kind, TimeOnly Time, string Reason, LeaveAttachment? Attachment, Caller Caller);

public sealed record DecideOvertimeCorrectionCommand(Guid Id, bool Approve, string? Note, Caller Caller);

public sealed record ListOvertimeCorrectionsQuery(string? Status, Caller Caller);

public sealed record GetOvertimeCorrectionAttachmentQuery(Guid Id, Caller Caller);

public static class CreateOvertimeCorrectionHandler
{
    public static async Task<Result<OvertimeCorrectionResult>> Handle(
        CreateOvertimeCorrectionCommand command,
        IReadRepository<OvertimeAssignment> assignments,
        IRepository<OvertimeCorrectionRequest> corrections,
        IReadRepository<AttendanceLog> logs,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        if (!Enum.TryParse<OvertimeCorrectionKind>(command.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            return new Result<OvertimeCorrectionResult>.Error("overtime_correction.kind", "Correction must be for a TapIn or a TapOut.");
        }

        var assignment = await assignments.FirstOrDefaultAsync(new OvertimeByIdSpec(new OvertimeAssignmentId(command.AssignmentId)), ct);
        if (assignment?.Employee is not { } subject)
        {
            return new Result<OvertimeCorrectionResult>.NotFound("Overtime was not found.");
        }

        // The employee asks for their own punch; nobody files this on someone's behalf.
        if (!OrgScope.IsSelf(command.Caller, subject))
        {
            return new Result<OvertimeCorrectionResult>.Error(ResultErrors.Forbidden, "You can only correct your own overtime.");
        }

        var instant = OvertimeCorrectionRules.Resolve(assignment, command.Time, policy);
        if (OvertimeCorrectionRules.Check(
                assignment, kind, instant, await OvertimeCorrectionRules.OwnedAsync(assignment, logs, policy, ct), policy)
            is { } error)
        {
            return new Result<OvertimeCorrectionResult>.Error(error.Code, error.Message);
        }

        if ((await corrections.ListAsync(new PendingCorrectionsSpec(assignment.Id), ct)).Any(c => c.Kind == kind))
        {
            return new Result<OvertimeCorrectionResult>.Error(
                "overtime_correction.duplicate", "A correction for this punch is already waiting for a decision.");
        }

        var request = OvertimeCorrectionRequest.Create(
            assignment.Id, subject.Id, assignment.Date, kind, instant, command.Reason,
            command.Attachment, command.Caller.UserId, clock.GetCurrentInstant());
        await corrections.AddAsync(request, ct);

        return new Result<OvertimeCorrectionResult>.Success(OvertimeMapper.ToResult(request, subject, canDecide: false));
    }
}

public static class DecideOvertimeCorrectionHandler
{
    public static async Task<Result<OvertimeCorrectionResult>> Handle(
        DecideOvertimeCorrectionCommand command,
        IRepository<OvertimeCorrectionRequest> corrections,
        IReadRepository<OvertimeAssignment> assignments,
        IReadRepository<Employee> employees,
        IReadRepository<AttendanceLog> logs,
        IRepository<AttendanceLog> logWriter,
        IRepository<AttendanceDay> days,
        AttendanceDayPolicy policy,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct)
    {
        var request = await corrections.FirstOrDefaultAsync(
            new CorrectionByIdSpec(new OvertimeCorrectionRequestId(command.Id)), ct);
        if (request?.Employee is not { } subject)
        {
            return new Result<OvertimeCorrectionResult>.NotFound("Correction request was not found.");
        }

        // Same standing as leave: the employee's own Manager or any Owner, never the requester.
        // Safe for a Manager because a correction can only move a punch inside a window an Owner approved.
        if (!LeaveRules.CanDecideFor(command.Caller, subject) || LeaveRules.IsRequester(command.Caller, request.RequestedByUserId))
        {
            return new Result<OvertimeCorrectionResult>.Error(ResultErrors.Forbidden, "You cannot decide this correction.");
        }

        var now = clock.GetCurrentInstant();
        if (!command.Approve)
        {
            request.Reject(command.Caller.UserId, command.Caller.Name, now, command.Note);
            await corrections.UpdateAsync(request, ct);
            return new Result<OvertimeCorrectionResult>.Success(OvertimeMapper.ToResult(request, subject, canDecide: false));
        }

        var assignment = await assignments.FirstOrDefaultAsync(
            new OvertimeByIdSpec(request.AssignmentId), ct);
        if (assignment is null)
        {
            return new Result<OvertimeCorrectionResult>.NotFound("The overtime this correction belongs to was not found.");
        }

        // Re-checked: the window or the punches may have moved since it was filed.
        if (OvertimeCorrectionRules.Check(
                assignment, request.Kind, request.PunchedAtUtc,
                await OvertimeCorrectionRules.OwnedAsync(assignment, logs, policy, ct), policy)
            is { } error)
        {
            return new Result<OvertimeCorrectionResult>.Error(error.Code, error.Message);
        }

        request.Approve(command.Caller.UserId, command.Caller.Name, now);
        await corrections.UpdateAsync(request, ct);

        // The decider writes the punch, so RecordedByUserId is the audit trail (decision 28).
        var written = await AttendanceLogService.RecordAsync(
            subject.Id.Value, request.PunchedAtUtc.ToDateTimeOffset(),
            request.Kind == OvertimeCorrectionKind.TapIn ? nameof(PunchType.In) : nameof(PunchType.Out),
            command.Caller.UserId, command.Caller.Name, null, $"Overtime correction: {request.Reason}",
            command.Caller, employees, logWriter, clock, bus, ct);
        if (written is Result<AttendanceResult>.Error failure)
        {
            // Not a Result.Error return: the request is already marked approved in this unit of work.
            throw new Erp.SharedKernel.Domain.Errors.DomainException(failure.Code, failure.Message);
        }

        await OvertimeEvaluator.RecomputeAroundAsync(assignment, logs, days, assignments, policy, ct);
        return new Result<OvertimeCorrectionResult>.Success(OvertimeMapper.ToResult(request, subject, canDecide: false));
    }
}

public static class ListOvertimeCorrectionsHandler
{
    public static async Task<Result<IReadOnlyList<OvertimeCorrectionResult>>> Handle(
        ListOvertimeCorrectionsQuery query, IReadRepository<OvertimeCorrectionRequest> corrections, CancellationToken ct)
    {
        OvertimeRequestStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<OvertimeRequestStatus>(query.Status, ignoreCase: true, out var parsed))
            {
                return new Result<IReadOnlyList<OvertimeCorrectionResult>>.Error("overtime_correction.status", "Unknown status.");
            }

            status = parsed;
        }

        var items = await corrections.ListAsync(new CorrectionListSpec(query.Caller, status), ct);
        return new Result<IReadOnlyList<OvertimeCorrectionResult>>.Success(items.Select(c => OvertimeMapper.ToResult(
            c, c.Employee!,
            c.Status == OvertimeRequestStatus.Pending && LeaveRules.CanDecideFor(query.Caller, c.Employee!)
                && !LeaveRules.IsRequester(query.Caller, c.RequestedByUserId))).ToList());
    }
}

/// <summary>The employee, the one who may decide it, or any Owner — and nobody else.</summary>
public static class GetOvertimeCorrectionAttachmentHandler
{
    public static async Task<Result<LeaveAttachmentContent>> Handle(
        GetOvertimeCorrectionAttachmentQuery query, IReadRepository<OvertimeCorrectionRequest> corrections,
        ILeaveAttachmentStorage storage, CancellationToken ct)
    {
        var request = await corrections.FirstOrDefaultAsync(
            new CorrectionByIdSpec(new OvertimeCorrectionRequestId(query.Id)), ct);
        if (request?.Employee is not { } subject)
        {
            return new Result<LeaveAttachmentContent>.NotFound("Correction request was not found.");
        }

        if (!(query.Caller.Role == EmployeeRole.Owner || OrgScope.IsSelf(query.Caller, subject)
              || LeaveRules.CanDecideFor(query.Caller, subject)))
        {
            return new Result<LeaveAttachmentContent>.Error(ResultErrors.Forbidden, "You cannot read this proof.");
        }

        var content = await storage.OpenAsync(request.Attachment.StorageKey, ct)
            ?? throw new Erp.SharedKernel.Domain.Errors.DomainException(
                "overtime.attachment_missing", "The proof is recorded on this request but is missing from storage.");
        return new Result<LeaveAttachmentContent>.Success(
            new LeaveAttachmentContent(content, request.Attachment.FileName, request.Attachment.ContentType));
    }
}

internal static class OvertimeCorrectionRules
{
    internal static async Task<IReadOnlyList<AttendanceLog>> OwnedAsync(
        OvertimeAssignment a, IReadRepository<AttendanceLog> logs, AttendanceDayPolicy policy, CancellationToken ct) =>
        OvertimeCalculator.OwnedPunches(a, await OvertimeEvaluator.LoadPunchesAsync([a], logs, policy, ct), policy);

    /// <summary>A time of day as the instant on the assignment's 05:00 → 05:00 day: before 05:00 is the next morning.</summary>
    internal static Instant Resolve(OvertimeAssignment a, TimeOnly time, AttendanceDayPolicy policy)
    {
        var local = LocalTime.FromTimeOnly(time);
        return (local < OvertimeAssignment.DayBoundary ? a.Date.PlusDays(1) : a.Date)
            .At(local).InZoneLeniently(policy.TimeZone).ToInstant();
    }

    /// <summary>
    /// Null when the punch can be written: the assignment is approved and open, the punch is
    /// really missing (a tap-in needs none, a tap-out needs exactly the tap-in), and the instant
    /// falls inside the window — after the tap-in for a tap-out.
    /// </summary>
    internal static (string Code, string Message)? Check(
        OvertimeAssignment a, OvertimeCorrectionKind kind, Instant at, IReadOnlyList<AttendanceLog> owned,
        AttendanceDayPolicy policy)
    {
        if (a.Status != OvertimeStatus.Approved || a.IsFrozen)
        {
            return ("overtime_correction.not_open", "Only an approved overtime in an open period can be corrected.");
        }

        if (!(kind == OvertimeCorrectionKind.TapIn ? owned.Count == 0 : owned.Count == 1))
        {
            return ("overtime_correction.not_missing",
                $"This overtime is not missing its {(kind == OvertimeCorrectionKind.TapIn ? "tap-in" : "tap-out")}.");
        }

        if (at < a.StartAt(policy.TimeZone) || at > a.EndAt(policy.TimeZone))
        {
            return ("overtime_correction.outside_window", "The time must fall inside the assigned window.");
        }

        return kind == OvertimeCorrectionKind.TapOut && at <= owned[0].PunchedAtUtc
            ? ("overtime_correction.before_tap_in", "The tap-out must be after the tap-in.")
            : null;
    }
}
