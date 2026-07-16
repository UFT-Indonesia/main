using FastEndpoints;

namespace Erp.Web.Endpoints.Leave;

public sealed class LeaveGroup : Group
{
    public LeaveGroup()
    {
        Configure("/api/leave", ep => ep.Description(x => x.WithTags("Leave")));
    }
}
