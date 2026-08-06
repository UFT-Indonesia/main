using Erp.Core.Aggregates.Attendance;

namespace Erp.UseCases.Attendance.Devices.RegisterDevice;

/// <summary>The one and only response that ever carries the plaintext secret.</summary>
public sealed class RegisterDeviceResult
{
    public Guid Id { get; init; }
    public string DeviceKey { get; init; } = default!;
    public string Name { get; init; } = default!;
    public bool Enabled { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string Secret { get; init; } = default!;

    public static RegisterDeviceResult From(AttendanceDevice device) => new()
    {
        Id = device.Id.Value,
        DeviceKey = device.DeviceKey,
        Name = device.Name,
        Enabled = device.Enabled,
        CreatedAtUtc = device.CreatedAtUtc.ToDateTimeOffset(),
        Secret = device.Secret,
    };
}
