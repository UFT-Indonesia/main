namespace Erp.UseCases.Attendance.Devices.RegisterDevice;

public sealed record RegisterDeviceCommand(string DeviceKey, string Name, Guid RegisteredByUserId);
