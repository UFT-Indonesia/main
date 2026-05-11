namespace Erp.Infrastructure.DeviceIngest;

public sealed class DeviceIngestOptions
{
    public const string SectionName = "DeviceIngest";

    public string HmacSecret { get; init; } = string.Empty;

    public int ToleranceSeconds { get; init; } = 300;
}
