using Erp.Core.Aggregates.Attendance;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class AttendancePolicyConfiguration : IEntityTypeConfiguration<AttendancePolicy>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    private static readonly ValueConverter<LocalTime, TimeOnly> LocalTimeConverter = new(
        localTime => TimeOnly.FromTimeSpan(TimeSpan.FromTicks(localTime.TickOfDay)),
        timeOnly => LocalTime.FromTicksSinceMidnight(timeOnly.Ticks));

    public void Configure(EntityTypeBuilder<AttendancePolicy> builder)
    {
        builder.ToTable("AttendancePolicies");

        builder.HasKey(policy => policy.Id);

        builder.Ignore(policy => policy.DomainEvents);

        builder.Property(policy => policy.Id)
            .HasConversion(new AttendancePolicyIdConverter());

        builder.Property(policy => policy.ShiftStart)
            .HasColumnName("shift_start")
            .HasConversion(LocalTimeConverter)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(policy => policy.ShiftEnd)
            .HasColumnName("shift_end")
            .HasConversion(LocalTimeConverter)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(policy => policy.ClockInGraceMinutes)
            .HasColumnName("clock_in_grace_minutes")
            .IsRequired();

        builder.Property(policy => policy.ClockOutGraceMinutes)
            .HasColumnName("clock_out_grace_minutes")
            .IsRequired();

        builder.Property(policy => policy.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(policy => policy.UpdatedByUserId)
            .HasColumnName("updated_by_user_id")
            .IsRequired();

        builder.Property(policy => policy.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
