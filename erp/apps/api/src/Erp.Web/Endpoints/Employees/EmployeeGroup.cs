using FastEndpoints;

namespace Erp.Web.Endpoints.Employees;

public sealed class EmployeeGroup : Group
{
    public EmployeeGroup()
    {
        Configure("/api/employees", ep => ep.Description(x => x.WithTags("Employees")));
    }
}
