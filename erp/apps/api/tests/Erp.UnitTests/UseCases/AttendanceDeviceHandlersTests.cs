using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Devices.Common;
using Erp.UseCases.Attendance.Devices.ListDevices;
using Erp.UseCases.Attendance.Devices.RegisterDevice;
using Erp.UseCases.Attendance.Devices.SetDeviceEnabled;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class AttendanceDeviceHandlersTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 8, 6, 9, 0);

    private readonly IRepository<AttendanceDevice> _devices = Substitute.For<IRepository<AttendanceDevice>>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public AttendanceDeviceHandlersTests()
    {
        _clock.GetCurrentInstant().Returns(Now);
        _devices.FirstOrDefaultAsync(Arg.Any<ISpecification<AttendanceDevice>>(), Arg.Any<CancellationToken>())
            .Returns((AttendanceDevice?)null);
    }

    private Task<Result<RegisterDeviceResult>> RegisterAsync(string key = "esp32-front-door", string name = "Front door") =>
        RegisterDeviceHandler.Handle(
            new RegisterDeviceCommand(key, name, Guid.NewGuid()), _devices, _clock, CancellationToken.None);

    [Fact]
    public async Task Register_persists_the_device_and_returns_its_secret_once()
    {
        var result = await RegisterAsync();

        var success = result.Should().BeOfType<Result<RegisterDeviceResult>.Success>().Subject;
        success.Value.DeviceKey.Should().Be("esp32-front-door");
        success.Value.Enabled.Should().BeTrue();
        success.Value.Secret.Should().NotBeNullOrWhiteSpace();
        await _devices.Received(1).AddAsync(Arg.Any<AttendanceDevice>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_generates_a_distinct_secret_per_device()
    {
        var first = await RegisterAsync(key: "device-a");
        var second = await RegisterAsync(key: "device-b");

        var firstSecret = ((Result<RegisterDeviceResult>.Success)first).Value.Secret;
        var secondSecret = ((Result<RegisterDeviceResult>.Success)second).Value.Secret;

        firstSecret.Should().NotBe(secondSecret);
    }

    [Fact]
    public async Task Register_rejects_a_device_key_that_is_already_taken()
    {
        var existing = AttendanceDevice.Register("esp32-front-door", "Existing", "secret", null, Now);
        _devices.FirstOrDefaultAsync(Arg.Any<ISpecification<AttendanceDevice>>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await RegisterAsync();

        result.Should().BeOfType<Result<RegisterDeviceResult>.Error>()
            .Which.Code.Should().Be("attendance_device.duplicate_key");
        await _devices.DidNotReceive().AddAsync(Arg.Any<AttendanceDevice>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "Front door")]
    [InlineData("   ", "Front door")]
    [InlineData("esp32", "")]
    public async Task Register_rejects_missing_key_or_name(string key, string name)
    {
        var result = await RegisterAsync(key, name);

        result.Should().BeOfType<Result<RegisterDeviceResult>.Error>();
        await _devices.DidNotReceive().AddAsync(Arg.Any<AttendanceDevice>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Disabling_a_device_persists_the_change()
    {
        var device = AttendanceDevice.Register("esp32", "Front door", "secret", null, Now);
        _devices.FirstOrDefaultAsync(Arg.Any<ISpecification<AttendanceDevice>>(), Arg.Any<CancellationToken>())
            .Returns(device);

        var result = await SetDeviceEnabledHandler.Handle(
            new SetDeviceEnabledCommand(device.Id.Value, false), _devices, CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceDeviceResult>.Success>()
            .Which.Value.Enabled.Should().BeFalse();
        device.Enabled.Should().BeFalse();
        await _devices.Received(1).UpdateAsync(device, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Re_enabling_a_device_persists_the_change()
    {
        var device = AttendanceDevice.Register("esp32", "Front door", "secret", null, Now);
        device.Disable();
        _devices.FirstOrDefaultAsync(Arg.Any<ISpecification<AttendanceDevice>>(), Arg.Any<CancellationToken>())
            .Returns(device);

        var result = await SetDeviceEnabledHandler.Handle(
            new SetDeviceEnabledCommand(device.Id.Value, true), _devices, CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceDeviceResult>.Success>()
            .Which.Value.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Setting_enabled_on_an_unknown_device_is_not_found()
    {
        var result = await SetDeviceEnabledHandler.Handle(
            new SetDeviceEnabledCommand(Guid.NewGuid(), false), _devices, CancellationToken.None);

        result.Should().BeOfType<Result<AttendanceDeviceResult>.NotFound>();
        await _devices.DidNotReceive().UpdateAsync(Arg.Any<AttendanceDevice>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Listing_never_exposes_device_secrets()
    {
        var reader = Substitute.For<IReadRepository<AttendanceDevice>>();
        reader.ListAsync(Arg.Any<ISpecification<AttendanceDevice>>(), Arg.Any<CancellationToken>())
            .Returns([AttendanceDevice.Register("esp32", "Front door", "super-secret", null, Now)]);

        var result = await ListDevicesHandler.Handle(new ListDevicesQuery(), reader, CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListDevicesResult>.Success>().Subject;
        var item = success.Value.Items.Should().ContainSingle().Subject;
        item.DeviceKey.Should().Be("esp32");
        // AttendanceDeviceResult has no Secret member at all — asserted structurally.
        typeof(AttendanceDeviceResult).GetProperty("Secret").Should().BeNull();
    }
}
