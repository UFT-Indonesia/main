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
using NodaTime;

namespace Erp.UseCases.Overtime.Rapels;

/// <summary>
/// An employee asks (<paramref name="Amount"/> must be null — they never name the figure); an
/// Owner adds one directly and must say the amount, which approves it on the spot.
/// </summary>
public sealed record CreateRapelCommand(
    Guid EmployeeId, DateOnly WorkDate, string Note, decimal? Amount, LeaveAttachment? Attachment, Caller Caller);

public sealed record ApproveRapelCommand(Guid Id, decimal Amount, Caller Caller);

public sealed record RejectRapelCommand(Guid Id, string? Note, Caller Caller);

public sealed record GetRapelAttachmentQuery(Guid Id, Caller Caller);

public static class CreateRapelHandler
{
    public static async Task<Result<RapelResult>> Handle(
        CreateRapelCommand command,
        IReadRepository<Employee> employees,
        IRepository<Core.Aggregates.Overtime.Rapel> rapels,
        IReadRepository<GajiPremiPeriod> periods,
        IClock clock,
        CancellationToken ct)
    {
        var employee = await employees.GetByIdAsync(new EmployeeId(command.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<RapelResult>.NotFound("Employee was not found.");
        }

        var isOwner = command.Caller.Role == EmployeeRole.Owner;
        if (!(isOwner || OrgScope.IsSelf(command.Caller, employee)) || employee.Role == EmployeeRole.Owner)
        {
            return new Result<RapelResult>.Error(ResultErrors.Forbidden, "You cannot file a rapel for this employee.");
        }

        var workDate = LocalDate.FromDateOnly(command.WorkDate);
        if (!await periods.AnyAsync(new ClosedPeriodByStartSpec(GajiPremiPeriod.StartOf(workDate)), ct))
        {
            return new Result<RapelResult>.Error(
                "rapel.period_open", "Only a closed period can be claimed. Use an overtime correction while it is still open.");
        }

        var now = clock.GetCurrentInstant();
        var rapel = Core.Aggregates.Overtime.Rapel.Create(
            employee.Id, workDate, command.Note, command.Attachment, command.Caller.UserId, now);
        if (isOwner)
        {
            rapel.Approve(
                command.Amount ?? throw new DomainException("rapel.amount", "An Owner adding a rapel must set the amount."),
                await GajiPremiRules.NextPayoutStartAsync(periods, ct), command.Caller.UserId, command.Caller.Name, now);
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
