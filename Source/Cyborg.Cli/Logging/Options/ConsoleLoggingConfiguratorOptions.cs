using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Aot.Modules.Composition;

namespace Cyborg.Cli.Logging;

[GeneratedDecomposition]
internal sealed partial record ConsoleLoggingConfiguratorOptions
(
    LogFormat Format = LogFormat.Text
) : LoggingConfiguratorOptions;