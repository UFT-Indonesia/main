using FastEndpoints;

namespace Erp.Web.Endpoints.Payroll;

/// <summary>Owner-only money pages. The handlers enforce the role; the menu only hides the entry.</summary>
public sealed class PayrollGroup : Group
{
    public PayrollGroup()
    {
        Configure("/api/payroll", ep => ep.Description(x => x.WithTags("Payroll")));
    }
}
