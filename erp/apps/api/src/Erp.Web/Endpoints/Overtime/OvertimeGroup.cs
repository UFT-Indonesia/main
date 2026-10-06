using FastEndpoints;

namespace Erp.Web.Endpoints.Overtime;

public sealed class OvertimeGroup : Group
{
    public OvertimeGroup()
    {
        Configure("/api/overtime", ep => ep.Description(x => x.WithTags("Overtime")));
    }
}
