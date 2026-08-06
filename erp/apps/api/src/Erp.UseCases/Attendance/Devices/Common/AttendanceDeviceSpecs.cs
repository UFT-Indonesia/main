using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.SharedKernel.Identity;

namespace Erp.UseCases.Attendance.Devices.Common;

internal sealed class AttendanceDeviceByKeySpec : SingleResultSpecification<AttendanceDevice>
{
    public AttendanceDeviceByKeySpec(string deviceKey)
    {
        Query.Where(device => device.DeviceKey == deviceKey);
    }
}

internal sealed class AllAttendanceDevicesSpec : Specification<AttendanceDevice>
{
    public AllAttendanceDevicesSpec()
    {
        Query.OrderBy(device => device.Name);
    }
}

internal sealed class AttendanceDeviceByIdSpec : SingleResultSpecification<AttendanceDevice>
{
    public AttendanceDeviceByIdSpec(AttendanceDeviceId id)
    {
        Query.Where(device => device.Id == id);
    }
}
