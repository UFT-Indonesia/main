using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

internal static class PayrollConverters
{
    internal static readonly ValueConverter<Instant, DateTimeOffset> Instants = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    internal static readonly ValueConverter<LocalDate, DateOnly> Dates = new(
        localDate => DateOnly.FromDateTime(localDate.ToDateTimeUnspecified()),
        dateOnly => LocalDate.FromDateTime(dateOnly.ToDateTime(TimeOnly.MinValue)));
}

public sealed class EmployeeSalaryHistoryConfiguration : IEntityTypeConfiguration<EmployeeSalaryHistory>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryHistory> builder)
    {
        builder.ToTable("EmployeeSalaryHistories");
        builder.HasKey(x => new { x.EmployeeId, x.EffectiveFrom });
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date");
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 0).IsRequired();
    }
}

public sealed class PayrollSettingsConfiguration : IEntityTypeConfiguration<PayrollSettings>
{
    public void Configure(EntityTypeBuilder<PayrollSettings> builder)
    {
        builder.ToTable("PayrollSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Divisor).HasColumnName("divisor").IsRequired();
        builder.Property(x => x.FirstMonth).HasColumnName("first_month")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date").IsRequired();
    }
}

public sealed class LeaveDeductionMonthConfiguration : IEntityTypeConfiguration<LeaveDeductionMonth>
{
    public void Configure(EntityTypeBuilder<LeaveDeductionMonth> builder)
    {
        builder.ToTable("LeaveDeductionMonths");
        builder.HasKey(x => x.Month);
        builder.Property(x => x.Month).HasColumnName("month")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date").ValueGeneratedNever();
        builder.Property(x => x.Divisor).HasColumnName("divisor").IsRequired();
        builder.Property(x => x.ClosedByUserId).HasColumnName("closed_by_user_id").IsRequired();
        builder.Property(x => x.ClosedByName).HasColumnName("closed_by_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClosedAtUtc).HasColumnName("closed_at_utc")
            .HasConversion(PayrollConverters.Instants).HasColumnType("timestamp with time zone").IsRequired();
    }
}

public sealed class LeaveDeductionLineConfiguration : IEntityTypeConfiguration<LeaveDeductionLine>
{
    public void Configure(EntityTypeBuilder<LeaveDeductionLine> builder)
    {
        builder.ToTable("LeaveDeductionLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.LeaveRequestId).HasColumnName("leave_request_id");
        builder.Property(x => x.Date).HasColumnName("date")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date");
        builder.Property(x => x.Month).HasColumnName("month")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date").IsRequired();
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.Type).HasColumnName("leave_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.FreeDays).HasColumnName("free_days").HasPrecision(8, 4).IsRequired();
        builder.Property(x => x.CutDays).HasColumnName("cut_days").HasPrecision(8, 4).IsRequired();
        builder.Property(x => x.Salary).HasColumnName("salary").HasPrecision(18, 0).IsRequired();
        builder.Property(x => x.DailyRate).HasColumnName("daily_rate").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.PaidAtClose).HasColumnName("paid_at_close").IsRequired();
        builder.Property(x => x.LateTargetMonth).HasColumnName("late_target_month")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date");
        builder.Property(x => x.SupersededAtUtc).HasColumnName("superseded_at_utc")
            .HasConversion(PayrollConverters.Instants).HasColumnType("timestamp with time zone");
        builder.Property(x => x.SupersededByName).HasColumnName("superseded_by_name").HasMaxLength(200);
        builder.Property(x => x.SupersededTargetMonth).HasColumnName("superseded_target_month")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date");
        builder.Ignore(x => x.ExactAmount);
        builder.Ignore(x => x.IsActive);
        builder.HasIndex(x => x.Month);
        builder.HasIndex(x => x.EmployeeId);

        // One live label per leave day; superseded ones stay as the record of what was paid.
        builder.HasIndex(x => new { x.LeaveRequestId, x.Date })
            .IsUnique()
            .HasFilter("superseded_at_utc IS NULL");
    }
}

public sealed class LeaveDeductionMonthExceptionConfiguration : IEntityTypeConfiguration<LeaveDeductionMonthException>
{
    public void Configure(EntityTypeBuilder<LeaveDeductionMonthException> builder)
    {
        builder.ToTable("LeaveDeductionMonthExceptions");
        builder.HasKey(x => new { x.Month, x.EmployeeId });
        builder.Property(x => x.Month).HasColumnName("month")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.FlatAmountPerDay).HasColumnName("flat_amount_per_day").HasPrecision(18, 0);
        builder.Property(x => x.Divisor).HasColumnName("divisor");
    }
}

public sealed class LeaveDeductionCorrectionConfiguration : IEntityTypeConfiguration<LeaveDeductionCorrection>
{
    public void Configure(EntityTypeBuilder<LeaveDeductionCorrection> builder)
    {
        builder.ToTable("LeaveDeductionCorrections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TargetMonth).HasColumnName("target_month")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date").IsRequired();
        builder.Property(x => x.SourceMonth).HasColumnName("source_month")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date").IsRequired();
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.LeaveRequestId).HasColumnName("leave_request_id").IsRequired();
        builder.Property(x => x.Date).HasColumnName("date")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date").IsRequired();
        builder.Property(x => x.Type).HasColumnName("leave_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.CutDays).HasColumnName("cut_days").HasPrecision(8, 4).IsRequired();
        builder.Property(x => x.DailyRate).HasColumnName("daily_rate").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ByUserId).HasColumnName("by_user_id").IsRequired();
        builder.Property(x => x.ByName).HasColumnName("by_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.AtUtc).HasColumnName("at_utc")
            .HasConversion(PayrollConverters.Instants).HasColumnType("timestamp with time zone").IsRequired();
        builder.Ignore(x => x.Amount);
        builder.HasIndex(x => x.TargetMonth);
        builder.HasIndex(x => x.SourceMonth);
    }
}

public sealed class LeaveDeductionAdjustmentConfiguration : IEntityTypeConfiguration<LeaveDeductionAdjustment>
{
    public void Configure(EntityTypeBuilder<LeaveDeductionAdjustment> builder)
    {
        builder.ToTable("LeaveDeductionAdjustments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Month).HasColumnName("month")
            .HasConversion(PayrollConverters.Dates).HasColumnType("date").IsRequired();
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 0).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ByUserId).HasColumnName("by_user_id").IsRequired();
        builder.Property(x => x.ByName).HasColumnName("by_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.AtUtc).HasColumnName("at_utc")
            .HasConversion(PayrollConverters.Instants).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => x.Month);
    }
}

public sealed class PayrollSettingsChangeConfiguration : IEntityTypeConfiguration<PayrollSettingsChange>
{
    public void Configure(EntityTypeBuilder<PayrollSettingsChange> builder)
    {
        builder.ToTable("PayrollSettingsChanges");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OldDivisor).HasColumnName("old_divisor").IsRequired();
        builder.Property(x => x.NewDivisor).HasColumnName("new_divisor").IsRequired();
        builder.Property(x => x.ChangedByUserId).HasColumnName("changed_by_user_id").IsRequired();
        builder.Property(x => x.ChangedByName).HasColumnName("changed_by_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ChangedAtUtc).HasColumnName("changed_at_utc")
            .HasConversion(PayrollConverters.Instants).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => x.ChangedAtUtc);
    }
}
