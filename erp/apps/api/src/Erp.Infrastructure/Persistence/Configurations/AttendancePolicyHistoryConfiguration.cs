using Erp.Core.Aggregates.Attendance;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class AttendancePolicyHistoryConfiguration : IEntityTypeConfiguration<AttendancePolicyHistory>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    private static readonly ValueConverter<LocalTime, TimeOnly> LocalTimeConverter = new(
        localTime => TimeOnly.FromTimeSpan(TimeSpan.FromTicks(localTime.TickOfDay)),
        timeOnly => LocalTime.FromTicksSinceMidnight(timeOnly.Ticks));

    public void Configure(EntityTypeBuilder<AttendancePolicyHistory> builder)
    {
        builder.ToTable("AttendancePolicyHistories");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.PolicyId)
            .HasColumnName("policy_id")
            .HasConversion(new AttendancePolicyIdConverter())
            .IsRequired();

        builder.Property(history => history.ShiftStart)
            .HasColumnName("shift_start")
            .HasConversion(LocalTimeConverter)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(history => history.ShiftEnd)
            .HasColumnName("shift_end")
            .HasConversion(LocalTimeConverter)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(history => history.ClockInGraceMinutes)
            .HasColumnName("clock_in_grace_minutes")
            .IsRequired();

        builder.Property(history => history.ClockOutGraceMinutes)
            .HasColumnName("clock_out_grace_minutes")
            .IsRequired();

        builder.Property(history => history.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(history => history.ChangedByUserId)
            .HasColumnName("changed_by_user_id")
            .IsRequired();

        builder.Property(history => history.ChangedAtUtc)
            .HasColumnName("changed_at_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(history => history.PolicyId);

        builder.HasOne<AttendancePolicy>()
            .WithMany()
            .HasForeignKey(history => history.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
