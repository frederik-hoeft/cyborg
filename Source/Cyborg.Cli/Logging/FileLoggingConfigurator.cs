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
        FileLoggingConfiguratorOptions options = configuration.Get(CliConfigurationDefaults.FILE_LOGGING_OPTIONS_KEY, CliConfigurationDefaults.FileLogging);
        if (!options.Enabled)
        {
            return;
        }

        builder.AddFilter<ZLoggerFileLoggerProvider>(null, options.MinimumLevel);

        if (File.Exists(options.Path))
        {
            File.Delete(options.Path);
        }
        builder.AddZLoggerFile(options.Path, fileOptions =>
        {
            if (options.Format is LogFormat.Json)
            {
                fileOptions.UseJsonFormatter(f => f.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
            }
        });
    }
}
