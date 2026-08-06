using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Persistence.ValueConverters;
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
        builder.ToTable("AttendanceLogs");

        builder.HasKey(log => log.Id);

        builder.Ignore(log => log.DomainEvents);

        builder.Property(log => log.Id)
            .HasConversion(new AttendanceLogIdConverter());

        builder.Property(log => log.EmployeeId)
            .HasColumnName("employee_id")
            .HasConversion(new EmployeeIdConverter())
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

        builder.Property(log => log.RecordedByUserId).HasColumnName("recorded_by_user_id");

        builder.HasIndex(log => new { log.EmployeeId, log.PunchedAtUtc });

        // Replay guard: a resent device request is byte-identical (same signed timestamp),
        // so it collides here instead of inserting a duplicate punch. Filtered to device
        // rows only — manual entries have no device_id and Postgres never treats NULLs as
        // equal, so they were already exempt, but the filter makes that explicit rather
        // than incidental.
        builder.HasIndex(log => new { log.EmployeeId, log.DeviceId, log.PunchedAtUtc })
            .IsUnique()
            .HasFilter("device_id IS NOT NULL");

        builder.HasOne(log => log.Employee)
            .WithMany()
            .HasForeignKey(log => log.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Notes live and die with their punch; append/remove only via the aggregate root.
        builder.HasMany(log => log.Notes)
            .WithOne()
            .HasForeignKey(note => note.AttendanceLogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(log => log.Notes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
