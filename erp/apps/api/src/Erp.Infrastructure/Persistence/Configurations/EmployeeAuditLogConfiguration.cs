using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class EmployeeAuditLogConfiguration : IEntityTypeConfiguration<EmployeeAuditLog>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    public void Configure(EntityTypeBuilder<EmployeeAuditLog> builder)
    {
        builder.ToTable("EmployeeAuditLogs");

        builder.HasKey(log => log.Id);

        builder.Property(log => log.EmployeeId)
            .HasColumnName("employee_id")
            .HasConversion(new EmployeeIdConverter())
            .IsRequired();

        builder.Property(log => log.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(log => log.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(log => log.OldValueJson)
            .HasColumnName("old_value_json")
            .HasColumnType("text");

        builder.Property(log => log.NewValueJson)
            .HasColumnName("new_value_json")
            .HasColumnType("text");

        builder.Property(log => log.ActorUserId)
            .HasColumnName("actor_user_id");

        builder.Property(log => log.ActorName)
            .HasColumnName("actor_name")
            .HasMaxLength(200);

        // Every query filters on employee and orders by time, so one composite covers both —
        // a standalone employee_id index would be redundant with this one's leading column.
        builder.HasIndex(log => new { log.EmployeeId, log.OccurredAtUtc })
            .IsDescending(false, true);
        builder.HasIndex(log => log.OccurredAtUtc);
        builder.HasIndex(log => log.EventType);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(log => log.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
