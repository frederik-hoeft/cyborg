using Cyborg.Cli.Logging;
using Cyborg.Cli.Metrics;
using Cyborg.Core.Modules.Debugging.Configuration;
using Cyborg.Core.Services.Security.Trust.Configuration;
using Microsoft.Extensions.Logging;

namespace Cyborg.Cli.Configuration;

internal static class CliConfigurationDefaults
{
    internal const string DEBUG_OPTIONS_KEY = "cyborg.core.debug";
    internal const string DEBUG_FRONTEND_KEY = DEBUG_OPTIONS_KEY + ".frontend";
    internal const string GLOBAL_LOGGING_OPTIONS_KEY = "cyborg.services.logging";
    internal const string GLOBAL_LOGGING_MINIMUM_LEVEL_KEY = GLOBAL_LOGGING_OPTIONS_KEY + ".minimum_level";
    internal const string ROLLING_LOGGING_OPTIONS_KEY = "cyborg.services.logging.rolling";
    internal const string ROLLING_LOGGING_ENABLED_KEY = ROLLING_LOGGING_OPTIONS_KEY + ".enabled";
    internal const string ROLLING_LOGGING_MINIMUM_LEVEL_KEY = ROLLING_LOGGING_OPTIONS_KEY + ".minimum_level";
    internal const string ROLLING_LOGGING_PATH_KEY = ROLLING_LOGGING_OPTIONS_KEY + ".path";
    internal const string ROLLING_LOGGING_INTERVAL_KEY = ROLLING_LOGGING_OPTIONS_KEY + ".rolling_interval";
    internal const string ROLLING_LOGGING_SIZE_BYTES_KEY = ROLLING_LOGGING_OPTIONS_KEY + ".rolling_size_bytes";
    internal const string ROLLING_LOGGING_FORMAT_KEY = ROLLING_LOGGING_OPTIONS_KEY + ".format";
    internal const string CONSOLE_LOGGING_OPTIONS_KEY = "cyborg.services.logging.console";
    internal const string CONSOLE_LOGGING_ENABLED_KEY = CONSOLE_LOGGING_OPTIONS_KEY + ".enabled";
    internal const string CONSOLE_LOGGING_MINIMUM_LEVEL_KEY = CONSOLE_LOGGING_OPTIONS_KEY + ".minimum_level";
    internal const string CONSOLE_LOGGING_FORMAT_KEY = CONSOLE_LOGGING_OPTIONS_KEY + ".format";
    internal const string FILE_LOGGING_OPTIONS_KEY = "cyborg.services.logging.file";
    internal const string FILE_LOGGING_ENABLED_KEY = FILE_LOGGING_OPTIONS_KEY + ".enabled";
    internal const string FILE_LOGGING_MINIMUM_LEVEL_KEY = FILE_LOGGING_OPTIONS_KEY + ".minimum_level";
    internal const string FILE_LOGGING_PATH_KEY = FILE_LOGGING_OPTIONS_KEY + ".path";
    internal const string FILE_LOGGING_FORMAT_KEY = FILE_LOGGING_OPTIONS_KEY + ".format";
    internal const string METRICS_OPTIONS_KEY = "cyborg.services.metrics";
    internal const string METRICS_NAMESPACE_KEY = METRICS_OPTIONS_KEY + ".namespace";
    internal const string METRICS_FILE_PATH_KEY = METRICS_OPTIONS_KEY + ".file_path";
    internal const string TRUST_OPTIONS_KEY = "cyborg.services.trust";
    internal const string TRUST_POLICIES_KEY = TRUST_OPTIONS_KEY + ".policies";
    internal const string TRUST_ENFORCEMENT_MODE_KEY = TRUST_OPTIONS_KEY + ".enforcement_mode";

    private const string CONSOLE_DEBUG_FRONTEND = "console";

    internal static DebugOptions Debug { get; } = DebugOptions.Default with { Frontend = CONSOLE_DEBUG_FRONTEND };

    internal static GlobalLoggingOptions GlobalLogging { get; } = GlobalLoggingOptions.Default with { MinimumLevel = LogLevel.Trace };

    internal static RollingFileLoggingConfiguratorOptions RollingLogging { get; } = RollingFileLoggingConfiguratorOptions.Default with { Enabled = false };

    internal static ConsoleLoggingConfiguratorOptions ConsoleLogging { get; } = ConsoleLoggingConfiguratorOptions.Default with { Enabled = false };

    internal static FileLoggingConfiguratorOptions FileLogging { get; } = FileLoggingConfiguratorOptions.Default with { Enabled = false };

    internal static MetricsOptions Metrics { get; } = MetricsOptions.Default;

    internal static ConfigurationTrustOptions Trust { get; } = ConfigurationTrustOptions.Default;

    internal static IReadOnlyDictionary<string, object> Values { get; } = new Dictionary<string, object>
    {
        [DEBUG_OPTIONS_KEY] = Debug,
        [GLOBAL_LOGGING_OPTIONS_KEY] = GlobalLogging,
        [ROLLING_LOGGING_OPTIONS_KEY] = RollingLogging,
        [CONSOLE_LOGGING_OPTIONS_KEY] = ConsoleLogging,
        [FILE_LOGGING_OPTIONS_KEY] = FileLogging,
        [METRICS_OPTIONS_KEY] = Metrics,
        [TRUST_OPTIONS_KEY] = Trust,
    };
}
