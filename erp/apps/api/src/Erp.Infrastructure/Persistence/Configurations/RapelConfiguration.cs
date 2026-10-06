using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Overtime;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class RapelConfiguration : IEntityTypeConfiguration<Rapel>
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

    public void Configure(EntityTypeBuilder<Rapel> builder)
    {
        builder.ToTable("Rapels");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Id).HasConversion(new RapelIdConverter());
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").HasConversion(new EmployeeIdConverter()).IsRequired();
        builder.Property(x => x.AssignmentId).HasColumnName("assignment_id").HasConversion(new OvertimeAssignmentIdConverter()).IsRequired();
        builder.Property(x => x.ClaimedStart).HasColumnName("claimed_start").HasConversion(LocalTimeConverter).HasColumnType("time").IsRequired();
        builder.Property(x => x.ClaimedEnd).HasColumnName("claimed_end").HasConversion(LocalTimeConverter).HasColumnType("time").IsRequired();
        builder.Property(x => x.ClaimedHours).HasColumnName("claimed_hours").IsRequired();
        builder.Property(x => x.SuggestedAmount).HasColumnName("suggested_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.WorkDate).HasColumnName("work_date").HasConversion(LocalDateConverter).HasColumnType("date").IsRequired();
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(LeaveRequest.ReasonMaxLength).IsRequired();
        // Owned, like leave's: a proof has no life apart from the one row it belongs to.
        builder.OwnsOne(x => x.Attachment, attachment =>
        {
            attachment.Property(a => a.StorageKey).HasColumnName("attachment_storage_key").HasMaxLength(300).IsRequired();
            attachment.Property(a => a.FileName).HasColumnName("attachment_file_name").HasMaxLength(LeaveAttachment.FileNameMaxLength).IsRequired();
            attachment.Property(a => a.ContentType).HasColumnName("attachment_content_type").HasMaxLength(100).IsRequired();
            attachment.Property(a => a.SizeBytes).HasColumnName("attachment_size_bytes").IsRequired();
        });
        builder.Navigation(x => x.Attachment).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.Property(x => x.PayoutPeriodStart).HasColumnName("payout_period_start").HasConversion(LocalDateConverter).HasColumnType("date");
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").HasConversion(InstantConverter).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(x => x.DecidedByName).HasColumnName("decided_by_name").HasMaxLength(200);
        builder.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc").HasConversion(InstantConverter).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DecisionNote).HasColumnName("decision_note").HasMaxLength(OvertimeAssignment.DecisionNoteMaxLength);

        builder.HasIndex(x => new { x.EmployeeId, x.Status });
        builder.HasIndex(x => x.PayoutPeriodStart);

        // One claim per overtime — a rejected one frees it for another try.
        builder.HasIndex(x => x.AssignmentId).IsUnique().HasFilter("status IN ('Pending', 'Approved')");

        builder.HasOne<OvertimeAssignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
