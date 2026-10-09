using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Auth;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Aggregates.Probation;
using Erp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<EmployeeAuditLog> EmployeeAuditLogs => Set<EmployeeAuditLog>();

    public DbSet<AttendanceLog> AttendanceLogs => Set<AttendanceLog>();

    public DbSet<AttendanceDevice> AttendanceDevices => Set<AttendanceDevice>();

    public DbSet<AttendanceDay> AttendanceDays => Set<AttendanceDay>();

    public DbSet<AttendancePolicy> AttendancePolicies => Set<AttendancePolicy>();

    public DbSet<AttendancePolicyHistory> AttendancePolicyHistories => Set<AttendancePolicyHistory>();

    public DbSet<Holiday> Holidays => Set<Holiday>();

    public DbSet<OvertimeAssignment> OvertimeAssignments => Set<OvertimeAssignment>();

    public DbSet<OvertimeCorrectionRequest> OvertimeCorrectionRequests => Set<OvertimeCorrectionRequest>();

    public DbSet<GajiPremiPeriod> GajiPremiPeriods => Set<GajiPremiPeriod>();

    public DbSet<Rapel> Rapels => Set<Rapel>();

    public DbSet<EmployeeSalaryHistory> EmployeeSalaryHistories => Set<EmployeeSalaryHistory>();

    public DbSet<PayrollSettings> PayrollSettings => Set<PayrollSettings>();

    public DbSet<LeaveDeductionMonth> LeaveDeductionMonths => Set<LeaveDeductionMonth>();

    public DbSet<LeaveDeductionLine> LeaveDeductionLines => Set<LeaveDeductionLine>();

    public DbSet<LeaveDeductionMonthException> LeaveDeductionMonthExceptions => Set<LeaveDeductionMonthException>();

    public DbSet<LeaveDeductionCorrection> LeaveDeductionCorrections => Set<LeaveDeductionCorrection>();

    public DbSet<LeaveDeductionAdjustment> LeaveDeductionAdjustments => Set<LeaveDeductionAdjustment>();

    public DbSet<PayrollSettingsChange> PayrollSettingsChanges => Set<PayrollSettingsChange>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    public DbSet<ProbationExtensionRequest> ProbationExtensionRequests => Set<ProbationExtensionRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("AuthUsers");
            entity.Property(user => user.FullName).HasMaxLength(200).IsRequired();
            // One account per employee; NULLs (the seeded owner) don't collide in Postgres.
            entity.HasIndex(user => user.EmployeeId).IsUnique();
        });

        builder.Entity<IdentityRole<Guid>>().ToTable("AuthRoles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("AuthUserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("AuthUserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("AuthUserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("AuthRoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("AuthUserTokens");
    }
}
