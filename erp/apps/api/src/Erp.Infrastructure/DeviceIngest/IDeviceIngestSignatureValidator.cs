namespace Erp.Infrastructure.DeviceIngest;

public interface IDeviceIngestSignatureValidator
{
    Task<DeviceIngestSignatureResult> ValidateAsync(
        string payload,
        string? deviceKey,
        string? timestamp,
        string? signature,
        CancellationToken ct);
}
