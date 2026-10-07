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
