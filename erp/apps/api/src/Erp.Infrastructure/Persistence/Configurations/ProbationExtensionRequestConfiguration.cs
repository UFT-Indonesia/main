using Erp.Core.Aggregates.Probation;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class ProbationExtensionRequestConfiguration
    : IEntityTypeConfiguration<ProbationExtensionRequest>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    private static readonly ValueConverter<LocalDate, DateOnly> LocalDateConverter = new(
        localDate => DateOnly.FromDateTime(localDate.ToDateTimeUnspecified()),
        dateOnly => LocalDate.FromDateTime(dateOnly.ToDateTime(TimeOnly.MinValue)));

    public void Configure(EntityTypeBuilder<ProbationExtensionRequest> builder)
    {
        builder.ToTable("ProbationExtensionRequests");

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Id)
            .HasConversion(new ProbationExtensionRequestIdConverter());

        builder.Property(request => request.EmployeeId)
            .HasColumnName("employee_id")
            .HasConversion(new EmployeeIdConverter())
            .IsRequired();

        builder.Property(request => request.CurrentEndsOn)
            .HasColumnName("current_ends_on")
            .HasConversion(LocalDateConverter)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(request => request.ProposedEndsOn)
            .HasColumnName("proposed_ends_on")
            .HasConversion(LocalDateConverter)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(request => request.Reason)
            .HasColumnName("reason")
            .HasMaxLength(ProbationExtensionRequest.ReasonMaxLength)
            .IsRequired();

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
            .HasMaxLength(ProbationExtensionRequest.DecisionNoteMaxLength);

        builder.HasIndex(request => new { request.EmployeeId, request.Status });

        builder.HasOne(request => request.Employee)
            .WithMany()
            .HasForeignKey(request => request.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
