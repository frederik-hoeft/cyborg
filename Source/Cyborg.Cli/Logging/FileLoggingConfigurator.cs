using Cyborg.Cli.Configuration;
using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Configuration;
using Cyborg.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ZLogger;
using ZLogger.Providers;

namespace Cyborg.Cli.Logging;

internal sealed class FileLoggingConfigurator(IConfiguration configuration) : ILoggingConfigurator
{
    public void Configure(ILoggingBuilder builder)
    {
        FileLoggingConfiguratorOptions defaults = CliConfigurationDefaults.FileLogging;
        bool enabled = configuration.Get(CliConfigurationDefaults.FILE_LOGGING_ENABLED_KEY, defaults.Enabled);
        if (!enabled)
        {
            return;
        }

        LogLevel minimumLevel = configuration.Get(CliConfigurationDefaults.FILE_LOGGING_MINIMUM_LEVEL_KEY, defaults.MinimumLevel);
        string path = configuration.Get(CliConfigurationDefaults.FILE_LOGGING_PATH_KEY, defaults.Path);
        LogFormat format = configuration.Get(CliConfigurationDefaults.FILE_LOGGING_FORMAT_KEY, defaults.Format);
        builder.AddFilter<ZLoggerFileLoggerProvider>(null, minimumLevel);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
        builder.AddZLoggerFile(path, fileOptions =>
        {
            if (format is LogFormat.Json)
            {
                fileOptions.UseJsonFormatter(f => f.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
            }
        });
    }
}
