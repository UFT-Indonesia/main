using Erp.Core.Aggregates.Attendance;

namespace Erp.UseCases.Attendance.Devices.Common;

/// <summary>Never carries the secret — that is shown exactly once, on the response to registration.</summary>
public sealed class AttendanceDeviceResult
{
    public Guid Id { get; init; }
    public string DeviceKey { get; init; } = default!;
    public string Name { get; init; } = default!;
    public bool Enabled { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }

    public static AttendanceDeviceResult From(AttendanceDevice device) => new()
    {
        Id = device.Id.Value,
        DeviceKey = device.DeviceKey,
        Name = device.Name,
        Enabled = device.Enabled,
        CreatedAtUtc = device.CreatedAtUtc.ToDateTimeOffset(),
    };
}
