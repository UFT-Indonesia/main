using Erp.UseCases.Common;
using Erp.UseCases.Overtime.Common;
using Erp.UseCases.Overtime.CreateOvertimeAssignment;
using Erp.UseCases.Overtime.DecideOvertimeAssignment;
using Erp.UseCases.Overtime.ListOvertimeAssignments;
using FastEndpoints;
using Wolverine;

namespace Erp.Web.Endpoints.Overtime;

public sealed class CreateOvertimeRequest
{
    public Guid EmployeeId { get; init; }
    public DateOnly Date { get; init; }

    /// <summary>Required on a day off; a weekday always starts at 18:30.</summary>
    public TimeOnly? StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
}

public sealed class ListOvertimeRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? Status { get; init; }
}

public sealed class DecideOvertimeRequest
{
    public Guid Id { get; init; }
    public string? Note { get; init; }
}

public sealed class EditOvertimeEndRequest
{
    public Guid Id { get; init; }
    public TimeOnly EndTime { get; init; }
}

/// <summary>Scoped by OvertimeRules.CanAssign: an Owner for anyone but an Owner, a Manager for their own Staff.</summary>
public sealed class CreateOvertimeEndpoint(IMessageBus bus) : BusEndpoint<CreateOvertimeRequest, OvertimeAssignmentResult>(bus)
{
    public override void Configure()
    {
        Post("/");
        Group<OvertimeGroup>();
    }

    protected override object Build(CreateOvertimeRequest r, Caller caller) =>
        new CreateOvertimeAssignmentCommand(r.EmployeeId, r.Date, r.StartTime, r.EndTime, caller);
}

public sealed class ListOvertimeEndpoint(IMessageBus bus) : BusEndpoint<ListOvertimeRequest, ListOvertimeAssignmentsResult>(bus)
{
    public override void Configure()
    {
        Get("/");
        Group<OvertimeGroup>();
    }

    protected override object Build(ListOvertimeRequest r, Caller caller) =>
        new ListOvertimeAssignmentsQuery(r.Page, r.PageSize, r.From, r.To, r.EmployeeId, r.Status, caller);
}

public sealed class ApproveOvertimeEndpoint(IMessageBus bus) : BusEndpoint<DecideOvertimeRequest, OvertimeAssignmentResult>(bus)
{
    public override void Configure()
    {
        Post("/{id:guid}/approve");
        Group<OvertimeGroup>();
    }

    protected override object Build(DecideOvertimeRequest r, Caller caller) => new ApproveOvertimeCommand(r.Id, caller);
}

public sealed class RejectOvertimeEndpoint(IMessageBus bus) : BusEndpoint<DecideOvertimeRequest, OvertimeAssignmentResult>(bus)
{
    public override void Configure()
    {
        Post("/{id:guid}/reject");
        Group<OvertimeGroup>();
    }

    protected override object Build(DecideOvertimeRequest r, Caller caller) => new RejectOvertimeCommand(r.Id, caller, r.Note);
}

public sealed class CancelOvertimeEndpoint(IMessageBus bus) : BusEndpoint<DecideOvertimeRequest, OvertimeAssignmentResult>(bus)
{
    public override void Configure()
    {
        Post("/{id:guid}/cancel");
        Group<OvertimeGroup>();
    }

    protected override object Build(DecideOvertimeRequest r, Caller caller) => new CancelOvertimeCommand(r.Id, caller, r.Note);
}

public sealed class EditOvertimeEndEndpoint(IMessageBus bus) : BusEndpoint<EditOvertimeEndRequest, OvertimeAssignmentResult>(bus)
{
    public override void Configure()
    {
        Post("/{id:guid}/edit");
        Group<OvertimeGroup>();
    }

    protected override object Build(EditOvertimeEndRequest r, Caller caller) => new EditOvertimeEndCommand(r.Id, r.EndTime, caller);
}
