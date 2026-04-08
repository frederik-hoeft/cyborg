using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Aot.Modules.Composition;
using Microsoft.Extensions.Logging;

namespace Cyborg.Cli.Logging;

[GeneratedDecomposition]
internal sealed partial record FileLoggingConfiguratorOptions
(
    LogLevel MinimumLevel = LogLevel.Information,
    string Path = "/var/log/cyborg/latest.log",
    LogFormat Format = LogFormat.Json
) : LoggingConfiguratorOptions;
