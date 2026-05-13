namespace Erp.Infrastructure.DeviceIngest;

public interface IDeviceIngestSignatureValidator
{
    DeviceIngestSignatureResult Validate(string payload, string? timestamp, string? signature);
}
