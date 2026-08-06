using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance;

/// <summary>
/// A physical punch reader (fingerprint/card device) authorized to submit attendance on
/// behalf of any employee. Deliberately not bound to one employee — a shared reader serves
/// the whole office — so the thing being authenticated is the device itself, not who it is
/// punching for.
/// </summary>
public sealed class AttendanceDevice : Entity<AttendanceDeviceId>
{
    // EF Core constructor.
    private AttendanceDevice() { }

    private AttendanceDevice(
        AttendanceDeviceId id,
        string deviceKey,
        string name,
        string secret,
        Guid? registeredByUserId,
        Instant createdAtUtc)
        : base(id)
    {
        DeviceKey = deviceKey;
        Name = name;
        Secret = secret;
        RegisteredByUserId = registeredByUserId;
        CreatedAtUtc = createdAtUtc;
        Enabled = true;
    }

    /// <summary>The identifier the physical device sends on every punch — what ties an incoming request to this row.</summary>
    public string DeviceKey { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Stored in cleartext. HMAC verification needs the raw key to recompute the signature —
    /// unlike a password or bearer token, a one-way hash cannot be used here. Same trust
    /// boundary as the JWT signing key, which already lives in plaintext config.
    /// </summary>
    public string Secret { get; private set; } = string.Empty;

    public bool Enabled { get; private set; }

    /// <summary>Null for devices backfilled from pre-existing punch history rather than registered by an Owner.</summary>
    public Guid? RegisteredByUserId { get; private set; }

    public Instant CreatedAtUtc { get; private set; }

    public static AttendanceDevice Register(
        string deviceKey,
        string name,
        string secret,
        Guid? registeredByUserId,
        Instant nowUtc)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
        {
            throw new DomainException("attendance_device.device_key", "Device key is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("attendance_device.name", "Device name is required.");
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new DomainException("attendance_device.secret", "Device secret is required.");
        }

        return new AttendanceDevice(
            AttendanceDeviceId.New(),
            deviceKey.Trim(),
            name.Trim(),
            secret,
            registeredByUserId,
            nowUtc);
    }

    public void Enable() => Enabled = true;

    public void Disable() => Enabled = false;
}
