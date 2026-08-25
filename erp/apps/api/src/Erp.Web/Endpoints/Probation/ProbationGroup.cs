using FastEndpoints;

namespace Erp.Web.Endpoints.Probation;

public sealed class ProbationGroup : Group
{
    public ProbationGroup()
    {
        Configure("/api/probation", ep => ep.Description(x => x.WithTags("Probation")));
    }
}
