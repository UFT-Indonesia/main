using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.GetLeaveAttachment;
using Erp.UseCases.Overtime.Common;
using Erp.UseCases.Overtime.GajiPremi;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Erp.UseCases.Overtime.Rapels;

/// <summary>
/// A claim on one approved assignment of a closed period. <paramref name="From"/>/<paramref name="To"/>
/// are the times worked (before 05:00 means the next morning). An employee leaves
/// <paramref name="Amount"/> null — they never name the figure; an Owner adding one directly may
/// set it, or leave it null to keep the suggested amount, and it is approved on the spot.
/// </summary>
public sealed record CreateRapelCommand(
    Guid AssignmentId, TimeOnly From, TimeOnly To, string Note, decimal? Amount, LeaveAttachment? Attachment, Caller Caller);

/// <summary>Null <paramref name="Amount"/> keeps the suggested figure.</summary>
public sealed record ApproveRapelCommand(Guid Id, decimal? Amount, Caller Caller);

public sealed record RejectRapelCommand(Guid Id, string? Note, Caller Caller);

public sealed record GetRapelAttachmentQuery(Guid Id, Caller Caller);

public static class CreateRapelHandler
{
    public static async Task<Result<RapelResult>> Handle(
        CreateRapelCommand command,
        IReadRepository<OvertimeAssignment> assignments,
        IRepository<Core.Aggregates.Overtime.Rapel> rapels,
        IReadRepository<GajiPremiPeriod> periods,
        AttendanceDayPolicy policy,
        IOptions<OvertimeOptions> options,
        IClock clock,
        CancellationToken ct)
    {
        var assignment = await assignments.FirstOrDefaultAsync(new OvertimeByIdSpec(new OvertimeAssignmentId(command.AssignmentId)), ct);
        if (assignment?.Employee is not { } employee)
        {
            return new Result<RapelResult>.NotFound("Overtime was not found.");
        }

        var isOwner = command.Caller.Role == EmployeeRole.Owner;
        if (!(isOwner || OrgScope.IsSelf(command.Caller, employee)) || employee.Role == EmployeeRole.Owner)
        {
            return new Result<RapelResult>.Error(ResultErrors.Forbidden, "You cannot file a rapel for this employee.");
        }

        if (!assignment.IsFrozen)
        {
            return new Result<RapelResult>.Error(
                "rapel.period_open", "Only a closed period can be claimed. Use an overtime correction while it is still open.");
        }

        // The Owner's own add is the route for anything later (GSS08 33c).
        var today = DisplayZone.Today(clock);
        if (!isOwner && today > GajiPremiRules.RapelDeadline(GajiPremiPeriod.StartOf(assignment.Date)))
        {
            return new Result<RapelResult>.Error(
                "rapel.deadline_passed", "The deadline to request a rapel for this period has passed. Contact the Owner.");
        }

        if (await rapels.AnyAsync(new LiveRapelForAssignmentSpec(assignment.Id), ct))
        {
            return new Result<RapelResult>.Error("rapel.duplicate", "This overtime already has a rapel.");
        }

        var now = clock.GetCurrentInstant();
        var rapel = Core.Aggregates.Overtime.Rapel.Create(
            assignment, LocalTime.FromTimeOnly(command.From), LocalTime.FromTimeOnly(command.To), command.Note,
            command.Attachment, policy, options.Value.Tiers, command.Caller.UserId, now);
        if (isOwner)
        {
            rapel.Approve(command.Amount, await GajiPremiRules.NextPayoutStartAsync(periods, ct), command.Caller.UserId, command.Caller.Name, now);
        }

        await rapels.AddAsync(rapel, ct);
        return new Result<RapelResult>.Success(OvertimeMapper.ToResult(rapel, employee));
    }
}

public static class ApproveRapelHandler
{
    public static Task<Result<RapelResult>> Handle(
        ApproveRapelCommand command, IRepository<Core.Aggregates.Overtime.Rapel> rapels,
        IReadRepository<GajiPremiPeriod> periods, IClock clock, CancellationToken ct) =>
        RapelDecision.DecideAsync(command.Id, command.Caller, rapels, async (r, now) =>
            r.Approve(command.Amount, await GajiPremiRules.NextPayoutStartAsync(periods, ct), command.Caller.UserId, command.Caller.Name, now),
            clock, ct);
}

public static class RejectRapelHandler
{
    public static Task<Result<RapelResult>> Handle(
        RejectRapelCommand command, IRepository<Core.Aggregates.Overtime.Rapel> rapels, IClock clock, CancellationToken ct) =>
        RapelDecision.DecideAsync(command.Id, command.Caller, rapels, (r, now) =>
            {
                r.Reject(command.Caller.UserId, command.Caller.Name, now, command.Note);
                return Task.CompletedTask;
            },
            clock, ct);
}

internal static class RapelDecision
{
    internal static async Task<Result<RapelResult>> DecideAsync(
        Guid id, Caller caller, IRepository<Core.Aggregates.Overtime.Rapel> rapels,
        Func<Core.Aggregates.Overtime.Rapel, Instant, Task> act, IClock clock, CancellationToken ct)
    {
        if (caller.Role != EmployeeRole.Owner)
        {
            return new Result<RapelResult>.Error(ResultErrors.Forbidden, "Only an Owner decides a rapel.");
        }

        var rapel = await rapels.FirstOrDefaultAsync(new RapelByIdSpec(new RapelId(id)), ct);
        if (rapel?.Employee is not { } subject)
        {
            return new Result<RapelResult>.NotFound("Rapel was not found.");
        }

        await act(rapel, clock.GetCurrentInstant());
        await rapels.UpdateAsync(rapel, ct);
        return new Result<RapelResult>.Success(OvertimeMapper.ToResult(rapel, subject));
    }
}

/// <summary>The WA proof: the Owner and the employee it is about.</summary>
public static class GetRapelAttachmentHandler
{
    public static async Task<Result<LeaveAttachmentContent>> Handle(
        GetRapelAttachmentQuery query, IReadRepository<Core.Aggregates.Overtime.Rapel> rapels,
        ILeaveAttachmentStorage storage, CancellationToken ct)
    {
        var rapel = await rapels.FirstOrDefaultAsync(new RapelByIdSpec(new RapelId(query.Id)), ct);
        if (rapel?.Employee is not { } subject)
        {
            return new Result<LeaveAttachmentContent>.NotFound("Rapel was not found.");
        }

        if (query.Caller.Role != EmployeeRole.Owner && !OrgScope.IsSelf(query.Caller, subject))
        {
            return new Result<LeaveAttachmentContent>.Error(ResultErrors.Forbidden, "You cannot read this proof.");
        }

        var content = await storage.OpenAsync(rapel.Attachment.StorageKey, ct)
            ?? throw new DomainException("rapel.attachment_missing", "The proof is recorded but is missing from storage.");
        return new Result<LeaveAttachmentContent>.Success(
            new LeaveAttachmentContent(content, rapel.Attachment.FileName, rapel.Attachment.ContentType));
    }
}
