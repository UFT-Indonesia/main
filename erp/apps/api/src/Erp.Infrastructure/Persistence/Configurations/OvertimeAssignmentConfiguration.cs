using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Overtime;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class OvertimeAssignmentConfiguration : IEntityTypeConfiguration<OvertimeAssignment>
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

    public void Configure(EntityTypeBuilder<OvertimeAssignment> builder)
    {
        builder.ToTable("OvertimeAssignments");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.IsFrozen);
        builder.Ignore(x => x.IsLive);
        builder.Ignore(x => x.OwnsPunches);
        builder.Ignore(x => x.EndDate);
        builder.Property(x => x.Id).HasConversion(new OvertimeAssignmentIdConverter());
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").HasConversion(new EmployeeIdConverter()).IsRequired();
        builder.Property(x => x.Date).HasColumnName("date").HasConversion(LocalDateConverter).HasColumnType("date").IsRequired();
        builder.Property(x => x.StartTime).HasColumnName("start_time").HasConversion(LocalTimeConverter).HasColumnType("time").IsRequired();
        builder.Property(x => x.EndTime).HasColumnName("end_time").HasConversion(LocalTimeConverter).HasColumnType("time").IsRequired();
        builder.Property(x => x.IsDayOff).HasColumnName("is_day_off").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").HasConversion(InstantConverter).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(x => x.DecidedByName).HasColumnName("decided_by_name").HasMaxLength(200);
        builder.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc").HasConversion(InstantConverter).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DecisionNote).HasColumnName("decision_note").HasMaxLength(OvertimeAssignment.DecisionNoteMaxLength);
        builder.Property(x => x.FrozenHours).HasColumnName("frozen_hours");
        builder.Property(x => x.FrozenAmount).HasColumnName("frozen_amount").HasPrecision(18, 2);
        builder.Property(x => x.FrozenAtUtc).HasColumnName("frozen_at_utc").HasConversion(InstantConverter).HasColumnType("timestamp with time zone");

        // One overtime per employee per date — rejected and cancelled ones free the date again.
        builder.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique()
            .HasFilter("status IN ('Pending', 'Approved', 'Expired')");
        builder.HasIndex(x => x.Date);

        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
