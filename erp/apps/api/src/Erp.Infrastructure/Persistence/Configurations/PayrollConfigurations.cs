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
        builder.HasKey(x => new { x.LeaveRequestId, x.Date });
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
        builder.Ignore(x => x.ExactAmount);
        builder.HasIndex(x => x.Month);
        builder.HasIndex(x => x.EmployeeId);
    }
}
