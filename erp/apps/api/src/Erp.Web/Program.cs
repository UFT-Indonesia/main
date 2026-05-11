using Erp.Infrastructure;
using Erp.Web.Endpoints.Attendance;
using Erp.Web.Authentication;
using Erp.Web.Endpoints.Auth;
using FastEndpoints.Swagger;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

    builder.Services
        .AddInfrastructure(builder.Configuration)
        .AddConfiguredJwtBearer(builder.Configuration)
        .AddCors(options =>
            options.AddPolicy("Web", policy =>
                policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                    .AllowAnyHeader()
                    .AllowAnyMethod()))
        .SwaggerDocument();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors("Web");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapAuthEndpoints();
    app.MapAttendanceEndpoints();
    app.UseSwaggerGen();

    app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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
