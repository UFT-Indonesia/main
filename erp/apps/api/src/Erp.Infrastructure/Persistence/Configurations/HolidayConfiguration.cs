using Erp.Core.Aggregates.Attendance;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    private static readonly ValueConverter<LocalDate, DateOnly> LocalDateConverter = new(
        localDate => DateOnly.FromDateTime(localDate.ToDateTimeUnspecified()),
        dateOnly => LocalDate.FromDateTime(dateOnly.ToDateTime(TimeOnly.MinValue)));

    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("Holidays");

        builder.HasKey(holiday => holiday.Id);

        builder.Ignore(holiday => holiday.DomainEvents);

        builder.Property(holiday => holiday.Id)
            .HasConversion(new HolidayIdConverter());

        builder.Property(holiday => holiday.Date)
            .HasColumnName("date")
            .HasConversion(LocalDateConverter)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(holiday => holiday.Name)
            .HasColumnName("name")
            .HasMaxLength(Holiday.NameMaxLength)
            .IsRequired();

        builder.Property(holiday => holiday.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(holiday => holiday.UpdatedByUserId)
            .HasColumnName("updated_by_user_id")
            .IsRequired();

        builder.Property(holiday => holiday.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(holiday => holiday.Date).IsUnique();
    }
}
