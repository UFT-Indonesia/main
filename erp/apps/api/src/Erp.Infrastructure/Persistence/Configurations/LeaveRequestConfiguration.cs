using Erp.Core.Aggregates.Leave;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    private static readonly ValueConverter<LocalDate, DateOnly> LocalDateConverter = new(
        localDate => DateOnly.FromDateTime(localDate.ToDateTimeUnspecified()),
        dateOnly => LocalDate.FromDateTime(dateOnly.ToDateTime(TimeOnly.MinValue)));

    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests");

        builder.HasKey(request => request.Id);

        builder.Ignore(request => request.DomainEvents);

        builder.Property(request => request.Id)
            .HasConversion(new LeaveRequestIdConverter());

        builder.Property(request => request.EmployeeId)
            .HasColumnName("employee_id")
            .HasConversion(new EmployeeIdConverter())
            .IsRequired();

        builder.Property(request => request.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(request => request.StartDate)
            .HasColumnName("start_date")
            .HasConversion(LocalDateConverter)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(request => request.EndDate)
            .HasColumnName("end_date")
            .HasConversion(LocalDateConverter)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(request => request.WorkdayCount)
            .HasColumnName("workday_count")
            .IsRequired();

        builder.Property(request => request.Reason)
            .HasColumnName("reason")
            .HasMaxLength(LeaveRequest.ReasonMaxLength);

        builder.Property(request => request.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(request => request.RequestedByUserId)
            .HasColumnName("requested_by_user_id")
            .IsRequired();

        builder.Property(request => request.RequestedAtUtc)
            .HasColumnName("requested_at_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(request => request.DecidedByUserId)
            .HasColumnName("decided_by_user_id");

        builder.Property(request => request.DecidedByName)
            .HasColumnName("decided_by_name")
            .HasMaxLength(200);

        builder.Property(request => request.DecidedAtUtc)
            .HasColumnName("decided_at_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone");

        builder.Property(request => request.DecisionNote)
            .HasColumnName("decision_note")
            .HasMaxLength(LeaveRequest.DecisionNoteMaxLength);

        builder.HasIndex(request => new { request.EmployeeId, request.Status });

        builder.HasOne(request => request.Employee)
            .WithMany()
            .HasForeignKey(request => request.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
