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
            entity.ToTable("auth_users");
            entity.Property(user => user.FullName).HasMaxLength(200).IsRequired();
            entity.HasIndex(user => user.EmployeeId);
        });

        builder.Entity<IdentityRole<Guid>>().ToTable("auth_roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("auth_user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("auth_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("auth_user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("auth_role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("auth_user_tokens");
    }
}
