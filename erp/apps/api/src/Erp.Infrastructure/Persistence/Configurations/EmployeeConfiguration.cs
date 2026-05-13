using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Persistence.ValueConverters;
using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    private static readonly ValueConverter<LocalDate, DateOnly> LocalDateConverter = new(
        localDate => DateOnly.FromDateTime(localDate.ToDateTimeUnspecified()),
        dateOnly => LocalDate.FromDateTime(dateOnly.ToDateTime(TimeOnly.MinValue)));

    private static readonly ValueConverter<EmployeeId?, Guid?> NullableEmployeeIdConverter = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new EmployeeId(value.Value) : null);

    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(employee => employee.Id);

        builder.Ignore(employee => employee.DomainEvents);

        builder.Property(employee => employee.Id)
            .HasConversion(new EmployeeIdConverter());

        builder.Property(employee => employee.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(employee => employee.Nik)
            .HasColumnName("nik")
            .HasConversion(nik => nik.Value, value => Nik.Create(value))
            .HasMaxLength(Nik.Length)
            .IsRequired();

        builder.Property(employee => employee.Npwp)
            .HasColumnName("npwp")
            .HasConversion(npwp => npwp == null ? null : npwp.Value, value => value == null ? null : Npwp.Create(value))
            .HasMaxLength(Npwp.NewLength);

        builder.OwnsOne(employee => employee.MonthlyWage, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName("monthly_wage_amount")
                .HasPrecision(18, 0)
                .IsRequired();

            money.Property(value => value.Currency)
                .HasColumnName("monthly_wage_currency")
                .HasMaxLength(Money.IDR.Length)
                .IsRequired();
        });

        builder.Property(employee => employee.EffectiveSalaryFrom)
            .HasColumnName("effective_salary_from")
            .HasConversion(LocalDateConverter)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(employee => employee.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(employee => employee.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(employee => employee.ParentId)
            .HasColumnName("parent_id")
            .HasConversion(NullableEmployeeIdConverter);

        builder.Property(employee => employee.TerminationDate)
            .HasColumnName("termination_date")
            .HasConversion(LocalDateConverter)
            .HasColumnType("date");

        builder.Navigation(employee => employee.MonthlyWage).IsRequired();
        builder.HasIndex(employee => employee.Nik).IsUnique();
        builder.HasIndex(employee => employee.ParentId);
    }
}
