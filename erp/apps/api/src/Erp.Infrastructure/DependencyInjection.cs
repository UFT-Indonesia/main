using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.Infrastructure.Attendance;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.DeviceIngest;
using Erp.Infrastructure.Identity;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Persistence.Hierarchy;
using Erp.SharedKernel.Identity;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;
using Wolverine.EntityFrameworkCore;

namespace Erp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt audience is required.")
            .Validate(options =>
            {
                if (string.IsNullOrWhiteSpace(options.SigningKey))
                {
                    return false;
                }
                try
                {
                    return Convert.FromBase64String(options.SigningKey).Length >= 32;
                }
                catch
                {
                    return false;
                }
            }, "Jwt signing key must be a valid Base64 string decoding to at least 32 bytes.")
            .Validate(options => options.AccessTokenMinutes > 0, "Jwt access token lifetime must be positive.")
            .Validate(options => options.RefreshTokenDays > 0, "Jwt refresh token lifetime must be positive.")
            .ValidateOnStart();

        services.AddOptions<DeviceIngestOptions>()
            .Bind(configuration.GetSection(DeviceIngestOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.HmacSecret), "Device ingest HMAC secret is required.")
            .Validate(options => options.ToleranceSeconds > 0, "Device ingest tolerance must be positive.")
            .ValidateOnStart();

        // The attendance shift/grace-period policy is now a DB row (admin-editable via
        // /api/attendance/policy), not static config. It's read fresh per DI scope — a
        // Hangfire job recomputes existing AttendanceDay rows whenever it changes, and
        // there's no hot-reload story needed for a value pulled per-request/per-message.
        services.AddScoped<AttendanceDayPolicy>(serviceProvider =>
        {
            var db = serviceProvider.GetRequiredService<AppDbContext>();
            var policy = db.AttendancePolicies.SingleOrDefault(p => p.Id == AttendancePolicyId.Singleton)
                ?? throw new InvalidOperationException(
                    "Attendance policy singleton row is missing. Seed it (or re-run migrations) before starting the app.");
            return policy.ToAttendanceDayPolicy();
        });

        services.AddOptions<IdentitySeedOptions>()
            .Bind(configuration.GetSection(IdentitySeedOptions.SectionName));

        services.AddOptions<ConnectionStringsOptions>()
            .Bind(configuration.GetSection(ConnectionStringsOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Default), "Default connection string is required.")
            .ValidateOnStart();

        services.AddSingleton<IClock>(SystemClock.Instance);

        services.AddDbContextWithWolverineIntegration<AppDbContext>((serviceProvider, options) =>
        {
            var connectionStrings = serviceProvider.GetRequiredService<IOptions<ConnectionStringsOptions>>();
            options.UseNpgsql(connectionStrings.Value.Default);
        });

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));
        services.AddScoped<IEmployeeHierarchyLookup, PgEmployeeHierarchyLookup>();

        // Hangfire: background job runner used to recompute AttendanceDay rows when the
        // attendance policy changes. Packages were already referenced but never wired up.
        services.AddHangfire((serviceProvider, config) =>
        {
            var connectionStrings = serviceProvider.GetRequiredService<IOptions<ConnectionStringsOptions>>();
            config.UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(connectionStrings.Value.Default));
        });
        services.AddHangfireServer();
        services.AddScoped<RecomputeAttendanceDaysJob>();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IDeviceIngestSignatureValidator, DeviceIngestSignatureValidator>();
        services.AddScoped<IdentitySeeder>();

        return services;
    }
}
