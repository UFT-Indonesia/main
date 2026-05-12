using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.DeviceIngest;
using Erp.Infrastructure.Identity;
using Erp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Erp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt audience is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey) && options.SigningKey.Length >= 32, "Jwt signing key must be at least 32 characters.")
            .Validate(options => options.AccessTokenMinutes > 0, "Jwt access token lifetime must be positive.")
            .ValidateOnStart();

        services.AddOptions<DeviceIngestOptions>()
            .Bind(configuration.GetSection(DeviceIngestOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.HmacSecret), "Device ingest HMAC secret is required.")
            .Validate(options => options.ToleranceSeconds > 0, "Device ingest tolerance must be positive.")
            .ValidateOnStart();

        services.AddOptions<IdentitySeedOptions>()
            .Bind(configuration.GetSection(IdentitySeedOptions.SectionName));

        services.AddSingleton<IClock>(SystemClock.Instance);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

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
        services.AddScoped<IDeviceIngestSignatureValidator, DeviceIngestSignatureValidator>();
        services.AddScoped<IdentitySeeder>();

        return services;
    }
}
