using Erp.Infrastructure;
using Erp.Infrastructure.Attendance;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Configuration;
using Erp.Infrastructure.DeviceIngest;
using Erp.Infrastructure.Exceptions;
using Erp.Infrastructure.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.Web.Middleware.Authentication;
using FastEndpoints;
using Hangfire;
using Hangfire.Dashboard;
using Scalar.AspNetCore;
using Serilog;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    builder.Configuration.AddDotEnvFile(builder.Environment.ContentRootPath);
    builder.Configuration.AddEnvironmentVariables();

    var connectionString = builder.Configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Failed to load connection string from configuration");
    }

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
        options.Discovery.IncludeAssembly(typeof(RefreshTokenService).Assembly);
        options.InvokeTracing = builder.Environment.IsDevelopment()
            ? InvokeTracingMode.Full
            : InvokeTracingMode.Lightweight;
            
        options.PersistMessagesWithPostgresql(connectionString);
        options.UseEntityFrameworkCoreTransactions();

        // Every handler that touches the DbContext runs in one EF Core transaction. Ardalis
        // repositories save on each call, so without this a handler with two writes can fail
        // between them and leave half its work committed. Messages the handler publishes are
        // outboxed into the same transaction, so a write and the event it raises land together.
        options.Policies.AutoApplyTransactions();

        // Local queues are in-memory by default: a handler throw or a restart silently drops
        // the message. The audit log can't afford that, so envelopes are persisted before
        // dispatch and retried.
        options.Policies.UseDurableLocalQueues();
    });

    builder.Services
        .AddProblemDetails()
        .AddExceptionHandler<DomainExceptionHandler>()
        .AddInfrastructure(builder.Configuration)
        .AddConfiguredJwtBearer(builder.Configuration)
        .AddCors(options =>
            options.AddPolicy("Web", policy =>
                policy.WithOrigins(corsAllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()))
        .AddFastEndpoints()
        .AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = "UFT ERP API";
                document.Info.Version = "v1";
                return Task.CompletedTask;
            });
        });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync();

        var deviceSeeder = scope.ServiceProvider.GetRequiredService<AttendanceDeviceSeeder>();
        await deviceSeeder.SeedAsync();
    }

    // TLS terminates at the reverse proxy (nginx/Caddy), which also does the HTTP→HTTPS
    // redirect. The app only emits HSTS so browsers refuse plain-HTTP on later visits.
    // UseHttpsRedirection is intentionally omitted — it loops behind a TLS-terminating proxy.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();
    app.UseCors("Web");
    app.UseAuthentication();
    app.UseMiddleware<MustChangePasswordMiddleware>();
    app.UseAuthorization();
    app.UseExceptionHandler();

    if (builder.Configuration.GetValue<bool>("Hangfire:DashboardEnabled"))
    {
        app.UseHangfireDashboard(
            builder.Configuration["Hangfire:DashboardPath"] ?? "/hangfire",
            new DashboardOptions
            {
                Authorization = [new HangfireDashboardAuthorizationFilter()],
            });
    }

    // Leave that simply ran out raises no event, so the OnLeave badge is re-derived on a
    // schedule. Hourly rather than daily-at-midnight: Hangfire's cron runs in the server's
    // zone, which need not be the shift zone the leave dates are read in, and the query only
    // touches employees whose flag could actually be wrong.
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<SyncEmployeeLeaveStatusJob>(
        "sync-employee-leave-status",
        job => job.RunAsync(CancellationToken.None),
        Cron.Hourly());

    // Safety net: a recompute message that exhausts its retries leaves a punch with no day,
    // which reads as Absent. Rebuilding from punches is idempotent, so correct days are
    // untouched. Pinned to the shift zone so "nightly" means night at the office, not on the
    // server. ponytail: zone hardcoded to the policy's current one — change both together.
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<RecomputeAttendanceDaysJob>(
        "recompute-attendance-days",
        job => job.RunAsync(CancellationToken.None),
        Cron.Daily(2),
        new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta") });

    app.UseFastEndpoints();

    // API docs expose the full endpoint surface + schemas — keep them off the public
    // internet in production. Dev only.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "UFT ERP API";
            options.Theme = ScalarTheme.Kepler;
        });
    }

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

public partial class Program;
