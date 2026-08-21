using Cyborg.Cli.Arguments;
using Cyborg.Cli.Configuration;
using Cyborg.Cli.Logging;
using Cyborg.Cli.Logging.Options;
using Cyborg.Cli.Metrics;
using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Logging;
using Jab;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZLogger.Providers;

namespace Cyborg.Cli;

[ServiceProviderModule]
[Singleton<LoggingOptions>]
[Singleton<DynamicArgumentLogRenderer>]
[Singleton<ILoggingConfigurator, ConsoleLoggingConfigurator>]
[Singleton<ILoggingConfigurator, RollingFileLoggingConfigurator>]
[Singleton<ILoggingConfigurator, FileLoggingConfigurator>]
[Singleton<IDynamicValueProvider, DynamicGlobalLoggingOptionsProvider>]
[Singleton<IDynamicValueProvider, DynamicRollingFileLoggingConfiguratorOptionsProvider>]
[Singleton<IDynamicValueProvider, DynamicFileLoggingConfiguratorOptionsProvider>]
[Singleton<IDynamicValueProvider, DynamicConsoleLoggingConfiguratorOptionsProvider>]
[Singleton<IDynamicValueProvider, DynamicMetricsOptionsProvider>]
[Singleton<IDynamicValueProvider, DynamicLogLevelProvider>]
[Singleton<IDynamicValueProvider, DynamicLogFormatProvider>]
[Singleton<IDynamicValueProvider, DynamicRollingIntervalProvider>]
[Singleton<ILoggerFactory>(Factory = nameof(CreateLoggerFactory))]
[Singleton<JsonConverter>(Factory = nameof(CreateRollingIntervalConverter))]
[Singleton<JsonConverter>(Factory = nameof(CreateLogFormatConverter))]
[Singleton<JsonConverter>(Factory = nameof(CreateLogLevelConverter))]
internal interface ICyborgCliServiceOptions
{
    static ILoggerFactory CreateLoggerFactory(IConfiguration configuration, IEnumerable<ILoggingConfigurator> configurators) => LoggerFactory.Create(builder =>
    {
        LogLevel minimumLevel = configuration.Get(CliConfigurationDefaults.GLOBAL_LOGGING_MINIMUM_LEVEL_KEY, CliConfigurationDefaults.GlobalLogging.MinimumLevel);
        builder.SetMinimumLevel(minimumLevel);
        foreach (ILoggingConfigurator configurator in configurators)
        {
            configurator.Configure(builder);
        }
    });

    static JsonConverter CreateRollingIntervalConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<RollingInterval>(namingPolicy);

    static JsonConverter CreateLogFormatConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<LogFormat>(namingPolicy);

    static JsonConverter CreateLogLevelConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<LogLevel>(namingPolicy);
}
