using Erp.Infrastructure.DeviceIngest;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.Infrastructure;

public sealed class DeviceIngestSignatureValidatorTests
{
    private const string Secret = "device-secret-for-tests";
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 11, 8, 0);

    [Fact]
    public void Validate_accepts_valid_signature()
    {
        var payload = "{\"employeeId\":\"14d583f0-c78d-4d9a-934e-af1e6ee523b1\"}";
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var signature = DeviceIngestSignatureValidator.ComputeSignature(payload, timestamp, Secret);
        var validator = CreateValidator();

        var result = validator.Validate(payload, timestamp, signature);

        result.IsValid.Should().BeTrue();
        result.FailureCode.Should().BeNull();
    }

    [Fact]
    public void Validate_rejects_invalid_signature()
    {
        var payload = "{}";
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var validator = CreateValidator();

        var result = validator.Validate(payload, timestamp, "bad-signature");

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be("device_ingest.signature_invalid");
    }

    [Fact]
    public void Validate_rejects_stale_timestamp()
    {
        var payload = "{}";
        var timestamp = Now.Minus(Duration.FromMinutes(10)).ToUnixTimeSeconds().ToString();
        var signature = DeviceIngestSignatureValidator.ComputeSignature(payload, timestamp, Secret);
        var validator = CreateValidator();

        var result = validator.Validate(payload, timestamp, signature);

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be("device_ingest.timestamp_out_of_tolerance");
    }

    private static DeviceIngestSignatureValidator CreateValidator()
    {
        var clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Now);

        return new DeviceIngestSignatureValidator(
            Options.Create(new DeviceIngestOptions
            {
                HmacSecret = Secret,
                ToleranceSeconds = 300,
            }),
            clock);
    }
}
