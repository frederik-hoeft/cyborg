using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Aot.Modules.Composition;
using Microsoft.Extensions.Logging;

namespace Cyborg.Cli.Logging;

[GeneratedDecomposition]
internal sealed partial record ConsoleLoggingConfiguratorOptions
(
    LogLevel MinimumLevel = LogLevel.Information,
    LogFormat Format = LogFormat.Text
) : LoggingConfiguratorOptions;
