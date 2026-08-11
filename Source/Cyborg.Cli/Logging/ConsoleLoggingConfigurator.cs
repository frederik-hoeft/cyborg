using Cyborg.Cli.Configuration;
using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Configuration;
using Cyborg.Core.Logging;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace Cyborg.Cli.Logging;

internal sealed class ConsoleLoggingConfigurator(IConfiguration configuration, LoggingOptions loggingOptions) : ILoggingConfigurator
{
    public void Configure(ILoggingBuilder builder)
    {
        ConsoleLoggingConfiguratorOptions defaults = CliConfigurationDefaults.ConsoleLogging;
        bool enabled = configuration.Get(CliConfigurationDefaults.CONSOLE_LOGGING_ENABLED_KEY, defaults.Enabled);
        if (!enabled)
        {
            return;
        }

        LogLevel configuredMinimumLevel = configuration.Get(CliConfigurationDefaults.CONSOLE_LOGGING_MINIMUM_LEVEL_KEY, defaults.MinimumLevel);
        LogLevel minimumLevel = loggingOptions.MinimumLevel ?? configuredMinimumLevel;
        LogFormat format = configuration.Get(CliConfigurationDefaults.CONSOLE_LOGGING_FORMAT_KEY, defaults.Format);
        builder.AddFilter<ZLoggerConsoleLoggerProvider>(category: null, minimumLevel);

        builder.AddZLoggerConsole(consoleOptions =>
        {
            consoleOptions.OutputEncodingToUtf8 = true;
            if (format is LogFormat.Json)
            {
                consoleOptions.UseJsonFormatter();
            }
            else
            {
                consoleOptions.UsePlainTextFormatter(formatter => formatter
                    .SetPrefixFormatter($"[{0:local-longdate}] {1} ({2}): ", static (in template, in info) => template
                        .Format(info.Timestamp, info.LogLevel, info.Category)));
            }
        });
    }
}
