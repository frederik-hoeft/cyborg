using Microsoft.Extensions.Logging;

namespace Cyborg.Cli.Logging;

internal sealed class LoggingOptions
{
    public LogLevel? MinimumLevel { get; set; }
}
