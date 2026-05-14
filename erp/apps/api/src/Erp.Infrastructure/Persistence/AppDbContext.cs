using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
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

    public DbSet<AttendanceLog> AttendanceLogs => Set<AttendanceLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("AuthUsers");
            entity.Property(user => user.FullName).HasMaxLength(200).IsRequired();
            entity.HasIndex(user => user.EmployeeId);
        });

        builder.Entity<IdentityRole<Guid>>().ToTable("AuthRoles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("AuthUserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("AuthUserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("AuthUserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("AuthRoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("AuthUserTokens");
    }
}
