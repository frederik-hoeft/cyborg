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
        ConsoleLoggingConfiguratorOptions options = configuration.Get("cyborg.services.logging.console", () => new ConsoleLoggingConfiguratorOptions() { Enabled = false });
        if (!options.Enabled)
        {
            return;
        }

        LogLevel minimumLevel = loggingOptions.MinimumLevel ?? options.MinimumLevel;
        builder.AddFilter<ZLoggerConsoleLoggerProvider>(null, minimumLevel);

        builder.AddZLoggerConsole(consoleOptions =>
        {
            consoleOptions.OutputEncodingToUtf8 = true;
            if (options.Format is LogFormat.Json)
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
