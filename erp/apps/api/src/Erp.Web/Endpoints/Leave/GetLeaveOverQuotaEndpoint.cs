using Erp.UseCases.Common;
using Erp.UseCases.Leave.GetLeaveOverQuota;
using FastEndpoints;
using Wolverine;

namespace Erp.Web.Endpoints.Leave;

/// <summary>Days of an unfiled leave that would be cut from salary, for the form's warning. Days only.</summary>
public sealed class GetLeaveOverQuotaEndpoint(IMessageBus bus)
    : BusEndpoint<GetLeaveOverQuotaRequest, LeaveOverQuotaResult>(bus)
{
    public override void Configure()
    {
        Get("/over-quota");
        Group<LeaveGroup>();
    }

    protected override object Build(GetLeaveOverQuotaRequest r, Caller caller) =>
        new GetLeaveOverQuotaQuery(r.EmployeeId, r.Type, r.StartDate, r.EndDate, r.HalfDay, r.StartHour, r.EndHour, caller, r.ExcludeRequestId);
}
