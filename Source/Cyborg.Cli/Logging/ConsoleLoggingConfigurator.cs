using Cyborg.Core.Logging;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Cli.Logging;

internal sealed class ConsoleLoggingConfigurator : ILoggingConfigurator
{
    public void Configure(ILoggingBuilder builder) =>
        builder.AddZLoggerConsole();
}
