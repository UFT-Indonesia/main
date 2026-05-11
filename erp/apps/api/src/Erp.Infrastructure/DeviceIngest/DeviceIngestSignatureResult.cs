namespace Erp.Infrastructure.DeviceIngest;

public sealed record DeviceIngestSignatureResult(bool IsValid, string? FailureCode)
{
    public static DeviceIngestSignatureResult Valid { get; } = new(true, null);

    public static DeviceIngestSignatureResult Invalid(string failureCode) => new(false, failureCode);
}
