using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Devices.Common;

namespace Erp.UseCases.Attendance.Devices.SetDeviceEnabled;

public static class SetDeviceEnabledHandler
{
    public static async Task<Result<AttendanceDeviceResult>> Handle(
        SetDeviceEnabledCommand command,
        IRepository<AttendanceDevice> devices,
        CancellationToken ct)
    {
        var device = await devices.FirstOrDefaultAsync(
            new AttendanceDeviceByIdSpec(new AttendanceDeviceId(command.Id)), ct);
        if (device is null)
        {
            return new Result<AttendanceDeviceResult>.NotFound("Device was not found.");
        }

        if (command.Enabled)
        {
            device.Enable();
        }
        else
        {
            device.Disable();
        }

        await devices.UpdateAsync(device, ct);

        return new Result<AttendanceDeviceResult>.Success(AttendanceDeviceResult.From(device));
    }
}
