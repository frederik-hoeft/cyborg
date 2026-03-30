using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Aot.Modules.Composition;

namespace Cyborg.Cli.Logging;

[GeneratedDecomposition]
internal sealed partial record FileLoggingConfiguratorOptions
(
    string Path = "/var/log/cyborg/latest.log",
    LogFormat Format = LogFormat.Json
) : LoggingConfiguratorOptions;