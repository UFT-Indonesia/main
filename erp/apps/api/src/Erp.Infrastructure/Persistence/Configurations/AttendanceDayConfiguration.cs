using Erp.Core.Aggregates.Attendance;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class AttendanceDayConfiguration : IEntityTypeConfiguration<AttendanceDay>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    private static readonly ValueConverter<LocalDate, DateOnly> LocalDateConverter = new(
        localDate => DateOnly.FromDateTime(localDate.ToDateTimeUnspecified()),
        dateOnly => LocalDate.FromDateTime(dateOnly.ToDateTime(TimeOnly.MinValue)));

    public void Configure(EntityTypeBuilder<AttendanceDay> builder)
    {
        builder.ToTable("AttendanceDays");

        builder.HasKey(day => day.Id);

        builder.Ignore(day => day.DomainEvents);

        builder.Property(day => day.Id)
            .HasConversion(new AttendanceDayIdConverter());

        builder.Property(day => day.EmployeeId)
            .HasColumnName("employee_id")
            .HasConversion(new EmployeeIdConverter())
            .IsRequired();

        builder.Property(day => day.CalendarDate)
            .HasColumnName("calendar_date")
            .HasConversion(LocalDateConverter)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(day => day.TapInUtc)
            .HasColumnName("tap_in_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone");

        builder.Property(day => day.TapOutUtc)
            .HasColumnName("tap_out_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone");

        builder.Property(day => day.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // One materialized row per employee per calendar day.
        builder.HasIndex(day => new { day.EmployeeId, day.CalendarDate }).IsUnique();

        builder.HasOne(day => day.Employee)
            .WithMany()
            .HasForeignKey(day => day.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
