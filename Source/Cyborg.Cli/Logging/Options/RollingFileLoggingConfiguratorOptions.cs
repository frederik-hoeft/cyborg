using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Aot.Modules.Composition;
using Microsoft.Extensions.Logging;
using ZLogger.Providers;

namespace Cyborg.Cli.Logging;

[GeneratedDecomposition]
internal sealed partial record RollingFileLoggingConfiguratorOptions
(
    string Path = "/var/log/cyborg",
    RollingInterval RollingInterval = RollingInterval.Day,
    int RollingSizeBytes = 10 * 1024 * 1024,
    LogFormat Format = LogFormat.Json
) : LoggingConfiguratorOptions;

internal abstract record LoggingConfiguratorOptions
{
    public bool Enabled { get; init; } = true;

    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;
}
