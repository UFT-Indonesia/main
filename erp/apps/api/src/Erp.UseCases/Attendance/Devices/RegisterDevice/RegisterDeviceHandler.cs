using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Devices.Common;
using NodaTime;

namespace Erp.UseCases.Attendance.Devices.RegisterDevice;

public static class RegisterDeviceHandler
{
    public static async Task<Result<RegisterDeviceResult>> Handle(
        RegisterDeviceCommand command,
        IRepository<AttendanceDevice> devices,
        IClock clock,
        CancellationToken ct)
    {
        var deviceKey = command.DeviceKey.Trim();
        if (await devices.FirstOrDefaultAsync(new AttendanceDeviceByKeySpec(deviceKey), ct) is not null)
        {
            return new Result<RegisterDeviceResult>.Error(
                "attendance_device.duplicate_key", "A device with this id is already registered.");
        }

        AttendanceDevice device;
        try
        {
            device = AttendanceDevice.Register(
                deviceKey,
                command.Name,
                DeviceSecretGenerator.Generate(),
                command.RegisteredByUserId,
                clock.GetCurrentInstant());
        }
        catch (DomainException ex)
        {
            return new Result<RegisterDeviceResult>.Error(ex.Code ?? "attendance_device.validation", ex.Message);
        }

        await devices.AddAsync(device, ct);

        return new Result<RegisterDeviceResult>.Success(RegisterDeviceResult.From(device));
    }
}
