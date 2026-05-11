using Erp.Core.Aggregates.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class AttendanceLogConfiguration : IEntityTypeConfiguration<AttendanceLog>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    public void Configure(EntityTypeBuilder<AttendanceLog> builder)
    {
        builder.ToTable("attendance_logs");

        builder.HasKey(log => log.Id);

        builder.Ignore(log => log.DomainEvents);

        builder.Property(log => log.EmployeeId)
            .HasColumnName("employee_id")
            .IsRequired();

        builder.Property(log => log.PunchedAtUtc)
            .HasColumnName("punched_at_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(log => log.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(log => log.PunchType)
            .HasColumnName("punch_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(log => log.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(100);

        builder.Property(log => log.Note)
            .HasColumnName("note")
            .HasMaxLength(500);

        builder.Property(log => log.RecordedByUserId).HasColumnName("recorded_by_user_id");

        builder.HasIndex(log => new { log.EmployeeId, log.PunchedAtUtc });
    }
}
