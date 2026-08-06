using Erp.UseCases.Attendance.Devices.Common;
using Erp.UseCases.Attendance.Devices.RegisterDevice;

namespace Erp.Web.Endpoints.Attendance.Devices;

public sealed class RegisterDeviceRequest
{
    /// <summary>The id the physical device will send on every punch (e.g. "esp32-front-door").</summary>
    public string DeviceKey { get; init; } = default!;

    public string Name { get; init; } = default!;
}

public sealed class AttendanceDeviceResponse
{
    public Guid Id { get; init; }
    public string DeviceKey { get; init; } = default!;
    public string Name { get; init; } = default!;
    public bool Enabled { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }

    public static AttendanceDeviceResponse From(AttendanceDeviceResult result) => new()
    {
        Id = result.Id,
        DeviceKey = result.DeviceKey,
        Name = result.Name,
        Enabled = result.Enabled,
        CreatedAtUtc = result.CreatedAtUtc,
    };
}

public sealed class RegisterDeviceResponse
{
    public AttendanceDeviceResponse Device { get; init; } = default!;

    /// <summary>Shown exactly once; not retrievable afterwards — copy it into the device now.</summary>
    public string Secret { get; init; } = default!;

    public static RegisterDeviceResponse From(RegisterDeviceResult result) => new()
    {
        Device = new AttendanceDeviceResponse
        {
            Id = result.Id,
            DeviceKey = result.DeviceKey,
            Name = result.Name,
            Enabled = result.Enabled,
            CreatedAtUtc = result.CreatedAtUtc,
        },
        Secret = result.Secret,
    };
}

public sealed class ListDevicesResponse
{
    public IReadOnlyList<AttendanceDeviceResponse> Items { get; init; } = [];
}

public sealed class SetDeviceEnabledRequest
{
    public Guid Id { get; init; }
    public bool Enabled { get; init; }
}
