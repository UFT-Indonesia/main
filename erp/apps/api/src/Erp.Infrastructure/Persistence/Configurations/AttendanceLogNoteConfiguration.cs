using Erp.Core.Aggregates.Attendance;
using Erp.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class AttendanceLogNoteConfiguration : IEntityTypeConfiguration<AttendanceLogNote>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    public void Configure(EntityTypeBuilder<AttendanceLogNote> builder)
    {
        builder.ToTable("AttendanceLogNotes");

        builder.HasKey(note => note.Id);

        // The Entity base ctor pre-assigns a client-side Guid, so the key is always set
        // before EF ever sees the instance. Without this, change detection reads the
        // set key as "existing row" and issues an UPDATE instead of an INSERT.
        builder.Property(note => note.Id).ValueGeneratedNever();

        builder.Property(note => note.AttendanceLogId)
            .HasColumnName("attendance_log_id")
            .HasConversion(new AttendanceLogIdConverter())
            .IsRequired();

        builder.Property(note => note.Text)
            .HasColumnName("text")
            .HasMaxLength(AttendanceLog.NoteMaxLength)
            .IsRequired();

        builder.Property(note => note.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(note => note.CreatedByName)
            .HasColumnName("created_by_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(note => note.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(note => note.AttendanceLogId);
    }
}
