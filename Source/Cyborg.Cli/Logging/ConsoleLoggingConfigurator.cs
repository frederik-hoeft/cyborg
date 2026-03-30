using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Configuration;
using Cyborg.Core.Logging;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Cli.Logging;

internal sealed class ConsoleLoggingConfigurator(IConfiguration configuration) : ILoggingConfigurator
{
    public void Configure(ILoggingBuilder builder)
    {
        ConsoleLoggingConfiguratorOptions options = configuration.Get("cyborg.services.logging.console", () => new ConsoleLoggingConfiguratorOptions() { Enabled = false });
        if (!options.Enabled)
        {
            return;
        }
        builder.AddZLoggerConsole(consoleOptions =>
        {
            consoleOptions.OutputEncodingToUtf8 = true;
            if (options.Format is LogFormat.Json)
            {
                consoleOptions.UseJsonFormatter();
            }
        });
    }
}
