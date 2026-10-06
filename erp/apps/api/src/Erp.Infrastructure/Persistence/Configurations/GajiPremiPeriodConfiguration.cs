using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Overtime;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class GajiPremiPeriodConfiguration : IEntityTypeConfiguration<GajiPremiPeriod>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    private static readonly ValueConverter<LocalDate, DateOnly> LocalDateConverter = new(
        localDate => DateOnly.FromDateTime(localDate.ToDateTimeUnspecified()),
        dateOnly => LocalDate.FromDateTime(dateOnly.ToDateTime(TimeOnly.MinValue)));

    private static readonly ValueConverter<LocalTime, TimeOnly> LocalTimeConverter = new(
        localTime => localTime.ToTimeOnly(),
        timeOnly => LocalTime.FromTimeOnly(timeOnly));

    public void Configure(EntityTypeBuilder<GajiPremiPeriod> builder)
    {
        builder.ToTable("GajiPremiPeriods");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Id).HasConversion(new GajiPremiPeriodIdConverter());
        builder.Property(x => x.StartDate).HasColumnName("start_date").HasConversion(LocalDateConverter).HasColumnType("date").IsRequired();
        builder.Property(x => x.ClosedByUserId).HasColumnName("closed_by_user_id").IsRequired();
        builder.Property(x => x.ClosedByName).HasColumnName("closed_by_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClosedAtUtc).HasColumnName("closed_at_utc").HasConversion(InstantConverter).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => x.StartDate).IsUnique();
    }
}
