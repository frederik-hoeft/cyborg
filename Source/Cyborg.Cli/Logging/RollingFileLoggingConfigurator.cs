using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Configuration;
using Cyborg.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ZLogger;

namespace Cyborg.Cli.Logging;

internal sealed class RollingFileLoggingConfigurator(IConfiguration configuration) : ILoggingConfigurator
{
    public void Configure(ILoggingBuilder builder)
    {
        RollingFileLoggingConfiguratorOptions options = configuration.Get("cyborg.services.logging.rolling", () => new RollingFileLoggingConfiguratorOptions() { Enabled = false });
        if (!options.Enabled)
        {
            return;
        }

        builder.AddFilter((providerName, _, level) =>
        {
            if (providerName?.Contains("Rolling", StringComparison.OrdinalIgnoreCase) is true)
            {
                return level >= options.MinimumLevel;
            }

            return true;
        });

        builder.AddZLoggerRollingFile(rollingOptions =>
        {
            rollingOptions.FilePathSelector = (timestamp, sequenceNumber) => Path.Join(options.Path, $"{timestamp.ToLocalTime():yyyy-MM-dd}_{sequenceNumber:000}.log");
            rollingOptions.RollingInterval = options.RollingInterval;
            rollingOptions.RollingSizeKB = options.RollingSizeBytes >> 10; // divide by 1024 to convert bytes to KB
            if (options.Format is LogFormat.Json)
            {
                rollingOptions.UseJsonFormatter(f => f.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
            }
        });
    }
}
