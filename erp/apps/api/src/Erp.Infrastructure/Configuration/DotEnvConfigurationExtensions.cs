using Microsoft.Extensions.Configuration;

namespace Erp.Infrastructure.Configuration;

public static class DotEnvConfigurationExtensions
{
    public static IConfigurationBuilder AddDotEnvFile(this IConfigurationBuilder configuration, string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, ".env");
        if (!File.Exists(path))
        {
            return configuration;
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
        return configuration;
    }
}
