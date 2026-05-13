using FastEndpoints;

namespace Erp.Web.Endpoints.Auth;

public sealed class AuthGroup : Group
{
    public AuthGroup()
    {
        Configure("/api/auth", ep => ep.Description(x => x.WithTags("Auth")));
    }
}
