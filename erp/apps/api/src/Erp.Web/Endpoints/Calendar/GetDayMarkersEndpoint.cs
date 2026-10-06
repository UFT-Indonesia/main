using Erp.UseCases.Calendar;
using Erp.UseCases.Common;
using Erp.Web.Endpoints.Overtime;
using FastEndpoints;
using Wolverine;

namespace Erp.Web.Endpoints.Calendar;

public sealed class CalendarGroup : Group
{
    public CalendarGroup()
    {
        Configure("/api/calendar", ep => ep.Description(x => x.WithTags("Calendar")));
    }
}

public sealed class GetDayMarkersRequest
{
    public Guid EmployeeId { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
}

/// <summary>Leave and OT dates of one employee, for marking date pickers. Kinds only — no reasons.</summary>
public sealed class GetDayMarkersEndpoint(IMessageBus bus) : BusEndpoint<GetDayMarkersRequest, DayMarkersResult>(bus)
{
    public override void Configure()
    {
        Get("/markers");
        Group<CalendarGroup>();
    }

    protected override object Build(GetDayMarkersRequest r, Caller caller) =>
        new GetDayMarkersQuery(r.EmployeeId, r.From, r.To, caller);
}
