using Erp.UseCases.Common;
using Erp.UseCases.Payroll;
using FastEndpoints;
using Wolverine;

namespace Erp.Web.Endpoints.Payroll;

public sealed class LeaveDeductionMonthRequest
{
    public DateOnly? Month { get; init; }
}

public sealed class CloseLeaveDeductionMonthRequest
{
    public DateOnly Month { get; init; }
}

public sealed class SetPayrollDivisorRequest
{
    public int Divisor { get; init; }
}

public sealed class AddLeaveDeductionAdjustmentRequest
{
    public DateOnly Month { get; init; }
    public Guid EmployeeId { get; init; }
    public decimal Amount { get; init; }
    public string Reason { get; init; } = default!;
}

public sealed class DeleteLeaveDeductionAdjustmentRequest
{
    public Guid Id { get; init; }
}

public sealed class GetLeaveDeductionMonthEndpoint(IMessageBus bus)
    : BusEndpoint<LeaveDeductionMonthRequest, LeaveDeductionMonthResult>(bus)
{
    public override void Configure()
    {
        Get("/leave-deductions");
        Group<PayrollGroup>();
    }

    protected override object Build(LeaveDeductionMonthRequest r, Caller caller) =>
        new GetLeaveDeductionMonthQuery(r.Month, caller);
}

public sealed class CloseLeaveDeductionMonthEndpoint(IMessageBus bus)
    : BusEndpoint<CloseLeaveDeductionMonthRequest, LeaveDeductionMonthResult>(bus)
{
    public override void Configure()
    {
        Post("/leave-deductions/close");
        Group<PayrollGroup>();
    }

    protected override object Build(CloseLeaveDeductionMonthRequest r, Caller caller) =>
        new CloseLeaveDeductionMonthCommand(r.Month, caller);
}

public sealed class SetPayrollDivisorEndpoint(IMessageBus bus)
    : BusEndpoint<SetPayrollDivisorRequest, PayrollSettingsResult>(bus)
{
    public override void Configure()
    {
        Put("/leave-deductions/divisor");
        Group<PayrollGroup>();
    }

    protected override object Build(SetPayrollDivisorRequest r, Caller caller) =>
        new SetPayrollDivisorCommand(r.Divisor, caller);
}

public sealed class GetPayrollDivisorHistoryEndpoint(IMessageBus bus)
    : BusEndpoint<EmptyRequest, IReadOnlyList<DivisorChangeResult>>(bus)
{
    public override void Configure()
    {
        Get("/leave-deductions/divisor-history");
        Group<PayrollGroup>();
    }

    protected override object Build(EmptyRequest r, Caller caller) => new GetPayrollDivisorHistoryQuery(caller);
}

public sealed class AddLeaveDeductionAdjustmentEndpoint(IMessageBus bus)
    : BusEndpoint<AddLeaveDeductionAdjustmentRequest, LeaveDeductionAdjustmentResult>(bus)
{
    public override void Configure()
    {
        Post("/leave-deductions/adjustments");
        Group<PayrollGroup>();
    }

    protected override object Build(AddLeaveDeductionAdjustmentRequest r, Caller caller) =>
        new AddLeaveDeductionAdjustmentCommand(r.Month, r.EmployeeId, r.Amount, r.Reason, caller);
}

public sealed class DeleteLeaveDeductionAdjustmentEndpoint(IMessageBus bus)
    : BusEndpoint<DeleteLeaveDeductionAdjustmentRequest, bool>(bus)
{
    public override void Configure()
    {
        Delete("/leave-deductions/adjustments/{id:guid}");
        Group<PayrollGroup>();
    }

    protected override object Build(DeleteLeaveDeductionAdjustmentRequest r, Caller caller) =>
        new DeleteLeaveDeductionAdjustmentCommand(r.Id, caller);
}
