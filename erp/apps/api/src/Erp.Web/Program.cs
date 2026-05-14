using Erp.Infrastructure;
using Erp.Infrastructure.Exceptions;
using Erp.Infrastructure.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.Web.Middleware.Authentication;
using FastEndpoints;
using Scalar.AspNetCore;
using Serilog;
using Wolverine;
using Wolverine.EntityFrameworkCore;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    if (builder.Environment.IsDevelopment() && !File.Exists(Path.Combine(builder.Environment.ContentRootPath, ".env")))
    {
        throw new InvalidOperationException("Missing environment variables.");
    }

    AddDotEnvFile(builder.Configuration, builder.Environment.ContentRootPath);
    builder.Configuration.AddEnvironmentVariables();

    var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    if (corsAllowedOrigins is null || corsAllowedOrigins.Length == 0)
    {
        throw new InvalidOperationException("Configuration value 'Cors:AllowedOrigins' must specify at least one origin.");
    }

    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

    builder.Host.UseWolverine(options =>
    {
        options.Discovery.IncludeAssembly(typeof(AttendanceResult).Assembly);
        options.InvokeTracing = builder.Environment.IsDevelopment()
            ? InvokeTracingMode.Full
            : InvokeTracingMode.Lightweight;

        options.UseEntityFrameworkCoreTransactions();

    });

    builder.Services
        .AddExceptionHandler<DomainExceptionHandler>()
        .AddInfrastructure(builder.Configuration)
        .AddConfiguredJwtBearer(builder.Configuration)
        .AddCors(options =>
            options.AddPolicy("Web", policy =>
                policy.WithOrigins(corsAllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()))
        .AddFastEndpoints()
        .AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = "UFT Davis ERP API";
                document.Info.Version = "v1";
                return Task.CompletedTask;
            });
        });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync();
    }

    app.UseSerilogRequestLogging();
    app.UseCors("Web");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseExceptionHandler();
    app.UseFastEndpoints();
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "UFT Davis ERP API";
        options.Theme = ScalarTheme.Kepler;
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static void AddDotEnvFile(ConfigurationManager configuration, string contentRootPath)
{
    var path = Path.Combine(contentRootPath, ".env");
    if (!File.Exists(path))
    {
        return;
    }

    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in File.ReadAllLines(path))
    {
        var trimmedLine = line.Trim();
        if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
        {
            continue;
        }

        if (trimmedLine.StartsWith("export ", StringComparison.Ordinal))
        {
            trimmedLine = trimmedLine["export ".Length..].TrimStart();
        }

        var separatorIndex = trimmedLine.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = trimmedLine[..separatorIndex].Trim().Replace("__", ":");
        var value = trimmedLine[(separatorIndex + 1)..].Trim().Trim('"', '\'');
        values[key] = value;
    }

    configuration.AddInMemoryCollection(values);
}

public partial class Program;
