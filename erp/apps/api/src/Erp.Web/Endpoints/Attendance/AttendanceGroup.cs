using FastEndpoints;

namespace Erp.Web.Endpoints.Attendance;

public sealed class AttendanceGroup : Group
{
    public AttendanceGroup()
    {
        Configure("/api/attendance", ep => ep.Description(x => x.WithTags("Attendance")));
    }
}
