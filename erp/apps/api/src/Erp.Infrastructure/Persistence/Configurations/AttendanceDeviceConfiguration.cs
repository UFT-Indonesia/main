using Erp.Core.Aggregates.Attendance;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class AttendanceDeviceConfiguration : IEntityTypeConfiguration<AttendanceDevice>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    public void Configure(EntityTypeBuilder<AttendanceDevice> builder)
    {
        builder.ToTable("AttendanceDevices");

        builder.HasKey(device => device.Id);

        builder.Property(device => device.Id)
            .HasConversion(new AttendanceDeviceIdConverter());

        builder.Property(device => device.DeviceKey)
            .HasColumnName("device_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(device => device.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(device => device.Secret)
            .HasColumnName("secret")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(device => device.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        builder.Property(device => device.RegisteredByUserId)
            .HasColumnName("registered_by_user_id");

        builder.Property(device => device.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(device => device.DeviceKey).IsUnique();
    }
}
