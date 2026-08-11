using Cyborg.Cli.Configuration;
using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Configuration;
using Cyborg.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ZLogger;
using ZLogger.Providers;

namespace Cyborg.Cli.Logging;

internal sealed class RollingFileLoggingConfigurator(IConfiguration configuration) : ILoggingConfigurator
{
    public void Configure(ILoggingBuilder builder)
    {
        RollingFileLoggingConfiguratorOptions defaults = CliConfigurationDefaults.RollingLogging;
        bool enabled = configuration.Get(CliConfigurationDefaults.ROLLING_LOGGING_ENABLED_KEY, defaults.Enabled);
        if (!enabled)
        {
            return;
        }

        LogLevel minimumLevel = configuration.Get(CliConfigurationDefaults.ROLLING_LOGGING_MINIMUM_LEVEL_KEY, defaults.MinimumLevel);
        string path = configuration.Get(CliConfigurationDefaults.ROLLING_LOGGING_PATH_KEY, defaults.Path);
        RollingInterval rollingInterval = configuration.Get(CliConfigurationDefaults.ROLLING_LOGGING_INTERVAL_KEY, defaults.RollingInterval);
        int rollingSizeBytes = configuration.Get(CliConfigurationDefaults.ROLLING_LOGGING_SIZE_BYTES_KEY, defaults.RollingSizeBytes);
        LogFormat format = configuration.Get(CliConfigurationDefaults.ROLLING_LOGGING_FORMAT_KEY, defaults.Format);
        builder.AddFilter<ZLoggerRollingFileLoggerProvider>(null, minimumLevel);

        builder.AddZLoggerRollingFile(rollingOptions =>
        {
            rollingOptions.FilePathSelector = (timestamp, sequenceNumber) => Path.Join(path, $"{timestamp.ToLocalTime():yyyy-MM-dd}_{sequenceNumber:000}.log");
            rollingOptions.RollingInterval = rollingInterval;
            rollingOptions.RollingSizeKB = rollingSizeBytes >> 10; // divide by 1024 to convert bytes to KB
            if (format is LogFormat.Json)
            {
                rollingOptions.UseJsonFormatter(f => f.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
            }
        });
    }
}
