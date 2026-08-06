namespace Erp.Infrastructure.DeviceIngest;

public sealed class DeviceIngestOptions
{
    public const string SectionName = "DeviceIngest";

    /// <summary>
    /// No longer used to validate incoming punches — each registered device has its own
    /// secret now. Kept only so <see cref="AttendanceDeviceSeeder"/> can backfill a device
    /// row, with this as its secret, for every device id already seen in punch history
    /// before this deployed. Safe to blank out once that backfill has run.
    /// </summary>
    public string HmacSecret { get; init; } = string.Empty;

    public int ToleranceSeconds { get; init; } = 300;
}
