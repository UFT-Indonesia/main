using Hangfire.Dashboard;

namespace Erp.Web.Middleware.Authentication;

/// <summary>Restricts the Hangfire dashboard to Owner/Manager, same as the attendance policy endpoints.</summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var user = context.GetHttpContext().User;
        return user.Identity?.IsAuthenticated == true
            && (user.IsInRole("Owner") || user.IsInRole("Manager"));
    }
}
