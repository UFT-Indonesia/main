using FastEndpoints;

namespace Erp.Web.Endpoints.Accounts;

public sealed class AccountsGroup : Group
{
    public AccountsGroup()
    {
        Configure("/api/accounts", ep => ep.Description(x => x.WithTags("Accounts")));
    }
}
