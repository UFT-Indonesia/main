using Erp.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Erp.Infrastructure.Persistence;

public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var contentRootPath = ResolveWebContentRootPath();

        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(contentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);

        configurationBuilder.AddDotEnvFile(contentRootPath);
        configurationBuilder.AddEnvironmentVariables();

        var configuration = configurationBuilder.Build();
        var services = new ServiceCollection()
            .AddOptions()
            .Configure<ConnectionStringsOptions>(configuration.GetRequiredSection(ConnectionStringsOptions.SectionName))
            .BuildServiceProvider();

        var connectionStrings = services.GetRequiredService<IOptions<ConnectionStringsOptions>>();
        if (string.IsNullOrWhiteSpace(connectionStrings.Value.Default))
        {
            throw new InvalidOperationException("Connection string 'Default' is required in configuration");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionStrings.Value.Default)
            .Options;

        return new AppDbContext(options);
    }

    private static string ResolveWebContentRootPath()
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var directory = currentDirectory; directory is not null; directory = directory.Parent)
        {
            var webProjectPath = Path.Combine(directory.FullName, "apps", "api", "src", "Erp.Web");
            if (File.Exists(Path.Combine(webProjectPath, "appsettings.json")))
            {
                return webProjectPath;
            }

            if (directory.Name.Equals("Erp.Infrastructure", StringComparison.OrdinalIgnoreCase))
            {
                var siblingWebProjectPath = Path.Combine(directory.Parent?.FullName ?? directory.FullName, "Erp.Web");
                if (File.Exists(Path.Combine(siblingWebProjectPath, "appsettings.json")))
                {
                    return siblingWebProjectPath;
                }
            }
        }

        throw new InvalidOperationException("⚠️ Could not locate `appsettings.json`");
    }

}
